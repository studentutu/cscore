using com.csutil.json;
using System;
using System.IO;
using Xunit;

namespace com.csutil.tests.json {

    public class JsonTests {

        public JsonTests(Xunit.Abstractions.ITestOutputHelper logger) { logger.UseAsLoggingOutput(); }

        class MyClass1 {
            public string myString;
            public string myString2 { get; set; }
        }

        [Fact]
        public void ExampleUsage1() {
            MyClass1 x1 = new MyClass1() { myString = "abc", myString2 = "def" };
            // Generate a json object from the object that includes all public fields and props:
            string jsonString = JsonWriter.GetWriter().Write(x1);
            // Parse the json back into a second instance and compare both:
            MyClass1 x2 = JsonReader.GetReader().Read<MyClass1>(jsonString);
            Assert.Equal(x1.myString, x2.myString);
            Assert.Equal(x1.myString2, x2.myString2);
            AssertV2.AreEqualJson(x1, x2);
        }

        private class MyClass2 {
        }

        private class MySubClass1 : MyClass2 {
            public string myString;
            public string myString2;
            public MySubClass1 myComplexField { get; set; }
            public MyClass2 myComplexField2 { get; set; }
        }

        private class MySubClass2_WithNoDefaultConstructor : MyClass2 {
            public string myString { get; set; }

            //The class has no default constructor but still can be instantiated by the JSON logic
            public MySubClass2_WithNoDefaultConstructor(string s) : base() { myString = s; }
        }

        [Fact]
        public void TestMissingDefaultConstructor() {
            var x1 = new MySubClass1() { myString = "I am s1", myString2 = "I am s2" };
            var jsonString = JsonWriter.GetWriter().Write(x1 as MyClass2);
            var x2 = JsonReader.GetReader().Read<MySubClass2_WithNoDefaultConstructor>(jsonString);
            Assert.Equal(x1.myString, x2.myString);
        }

        [Fact]
        public void TestWithTypedJson() {
            // Typed json includes the C# assembly types in the json, so works only in a C# only scenario to parse the
            // json string back into the correct C# class
            MySubClass1 x1 = new MySubClass1() { myString = "I am s1", myComplexField2 = new MySubClass1() { myString = "A2" } };
            string json = TypedJsonHelper.NewTypedJsonWriter().Write(x1);
            object x2 = TypedJsonHelper.NewTypedJsonReader().Read<object>(json);
            Assert.True(x2 is MySubClass1);
            var x3 = x2 as MySubClass1;
            Assert.Equal(x3.myString, x1.myString);
            Assert.Equal((x3.myComplexField2 as MySubClass1).myString, (x1.myComplexField2 as MySubClass1).myString);
        }

        [Fact]
        public void TestMultipleJsonWriters1() {
            // Force the default json writer to be injected before the custom one is setup:
            Assert.Equal("{}", JsonWriter.AsPrettyString(new MyClass2()));
            TestMultipleJsonWriters2();
        }

        [Fact]
        public void TestMultipleJsonWriters2() {
            // Register a custom writer that handles only MyClass1 conversions:
            IJsonWriter myClass1JsonConverter = new MyClass1JsonConverter();
            IJsonWriter defaultWriter = JsonWriter.GetWriter(this);
            IoC.inject.RegisterInjector(this, (caller, createIfNull) => {
                if (caller is MyClass1) { return myClass1JsonConverter; }
                return defaultWriter; // Fallback to default JsonWriter  
            });

            // Converting MyClass1 instances to json will now always use the MyClass1JsonConverter:
            Assert.Equal("[]", JsonWriter.AsPrettyString(new MyClass1()));
            // Other json conversions still work as usual
            Assert.Equal("{}", JsonWriter.AsPrettyString(new MyClass2()));

            // Ensure that the additional create json writer for MyClass2 did not delete MyClass1JsonConverter:   
            Assert.Equal("[]", JsonWriter.AsPrettyString(new MyClass1()));

            IoC.inject.UnregisterInjector<IJsonWriter>(this);
            Assert.Equal("{}", JsonWriter.AsPrettyString(new MyClass2()));
            Assert.NotEqual("[]", JsonWriter.AsPrettyString(new MyClass1()));
        }

        /// <summary> Dummy json converter that always returns an emtpy list: [] </summary>
        private class MyClass1JsonConverter : IJsonWriter {
            public string Write(object data) { return "[]"; }
            public void Write(object data, StreamWriter streamWriter) { throw Log.e("Not supported"); }
        }

    }

}
