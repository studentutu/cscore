using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using com.csutil.http.apis;

namespace com.csutil.keyvaluestore {

    public class GoogleSheetsKeyValueStore : IKeyValueStore {

        public IKeyValueStore fallbackStore { get; set; }
        public long latestFallbackGetTimingInMs { get { return fallbackStore.latestFallbackGetTimingInMs; } set { } }

        private readonly string apiKey;
        private readonly string spreadsheetId;

        private string _sheetName;
        public string sheetName {
            get { return _sheetName; }
            set {
                if (value != _sheetName) { InitDebouncedDownloadLogic(); }
                _sheetName = value;
            }
        }

        public Func<Task<bool>> dowloadOnlineDataDebounced { get; private set; }

        private readonly double delayInMsBetweenCheck;
        private List<List<string>> latestRawSheetData { get; set; }

        public GoogleSheetsKeyValueStore(IKeyValueStore localCache, string apiKey, string spreadsheetId, string sheetName, double delayInMsBetweenCheck = 10000) {
            this.fallbackStore = localCache;
            this.delayInMsBetweenCheck = delayInMsBetweenCheck;
            InitDebouncedDownloadLogic();
            this.apiKey = apiKey;
            this.spreadsheetId = spreadsheetId;
            this._sheetName = sheetName;
        }

        private void InitDebouncedDownloadLogic() {
            latestRawSheetData = null;
            // Create a debounced func that only downloads new data max every 10 seconds and
            // trigger this method only if inet available:
            Func<Task> t = async () => {
                if (latestRawSheetData.IsNullOrEmpty()) {
                    await InternetStateManager.Instance(this).HasInetAsync;
                }
                if (InternetStateManager.Instance(this).HasInet) {
                    await DowloadOnlineData();
                }
                ThrowIfSheetDataMissing();
            };
            dowloadOnlineDataDebounced = t.AsThrottledDebounceV2(delayInMsBetweenCheck);
        }

        private void ThrowIfSheetDataMissing() {
            if (latestRawSheetData == null) { throw new Exception("Could not download Google Sheet data"); }
        }

        public DisposeState IsDisposed { get; private set; } = DisposeState.Active;

        public void Dispose() {
            IsDisposed = DisposeState.DisposingStarted;
            fallbackStore?.Dispose();
            IsDisposed = DisposeState.Disposed;
        }

        private async Task DowloadOnlineData() {
            var newRawSheetData = await GoogleSheetsV4.GetSheet(apiKey, spreadsheetId, sheetName);
            if (!latestRawSheetData.IsNullOrEmpty()) {
                foreach (var entry in ParseRawSheetData(FilterForChanges(latestRawSheetData, newRawSheetData))) {
                    await fallbackStore.Set(entry.Key, entry.Value);
                }
            } else { // Assuming that write is much more expensive then read:
                foreach (var newEntry in ParseRawSheetData(newRawSheetData)) {
                    var oldEntry = await fallbackStore.Get<object>(newEntry.Key, null);
                    if (!JsonWriter.HasEqualJson(oldEntry, newEntry)) {
                        await fallbackStore.Set(newEntry.Key, newEntry.Value);
                    }
                }
            }
            latestRawSheetData = newRawSheetData;
        }

        private async Task DownloadOnlineDataIfNeeded() {
            var t = dowloadOnlineDataDebounced();
            if (latestRawSheetData == null) { await t; }
        }

        private static List<List<string>> FilterForChanges(List<List<string>> oldData, List<List<string>> newData) {
            var filtered = new List<List<string>>();
            for (int i = 0; i < newData.Count; i++) {
                var newLine = newData[i];
                if (oldData.Count <= i || ChangeFound(oldData[i], newLine)) { filtered.Add(newLine); }
            }
            return filtered;
        }

        private static bool ChangeFound(List<string> oldLine, List<string> newLine) {
            if (oldLine.Count != newLine.Count) { return true; }
            for (int i = 0; i < newLine.Count; i++) {
                if (oldLine[i] != newLine[i]) { return true; }
            }
            return false;
        }

        private Dictionary<string, object> ParseRawSheetData(List<List<string>> rawSheetData) {
            var result = new Dictionary<string, object>();
            if (rawSheetData.IsNullOrEmpty()) { return result; }
            var fieldNames = rawSheetData.First().ToList();
            foreach (var column in rawSheetData.Skip(1)) {
                result.Add(column.First(), ToObject(fieldNames, column.ToList()));
            }
            return result;
        }

        private object ToObject(List<string> names, List<string> values) {
            var nc = names.Count();
            var vCount = values.Count();
            if (nc < vCount) { throw new IndexOutOfRangeException($"Only {nc} names but {vCount} values in row"); }
            var result = new Dictionary<string, object>();
            var jsonReader = JsonReader.GetReader();
            for (int i = 0; i < vCount; i++) { AddToResult(result, jsonReader, names[i], values[i].Trim()); }
            return result;
        }

        private bool AddToResult(Dictionary<string, object> result, IJsonReader jsonReader, string fieldName, string value) {
            if (value.IsNullOrEmpty()) { return false; }
            try {
                if (value.StartsWith("{") && value.EndsWith("}")) {
                    result.Add(fieldName, jsonReader.Read<Dictionary<string, object>>(value));
                    return true;
                }
                if (value.StartsWith("[") && value.EndsWith("]")) {
                    result.Add(fieldName, jsonReader.Read<List<object>>(value));
                    return true;
                }
            } catch (Exception e) { Log.e(e); }
            result.Add(fieldName, ParsePrimitive(value));
            return true;
        }

        private static object ParsePrimitive(string value) {
            if (bool.TryParse(value, out bool b)) { return b; }
            if (int.TryParse(value, out int i)) { return i; }
            if (DoubleTryParseV2(value, out double d)) { return d; }
            return value;
        }

        private static bool DoubleTryParseV2(string value, out double d) {
            return double.TryParse(value.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out d);
        }

        public async Task<bool> ContainsKey(string key) {
            await DownloadOnlineDataIfNeeded();
            return await fallbackStore.ContainsKey(key);
        }

        public async Task<T> Get<T>(string key, T defaultValue) {
            await DownloadOnlineDataIfNeeded();
            return Mapper.Map<T>(await fallbackStore.Get<object>(key, defaultValue));
        }

        public async Task<IEnumerable<string>> GetAllKeys() {
            await DownloadOnlineDataIfNeeded();
            return await fallbackStore.GetAllKeys();
        }

        public Task<bool> Remove(string key) { throw new NotSupportedException(this + " is a readonly store"); }

        public Task RemoveAll() { throw new NotSupportedException(this + " is a readonly store"); }

        public Task<object> Set(string key, object value) { throw new NotSupportedException(this + " is a readonly store"); }

    }

}