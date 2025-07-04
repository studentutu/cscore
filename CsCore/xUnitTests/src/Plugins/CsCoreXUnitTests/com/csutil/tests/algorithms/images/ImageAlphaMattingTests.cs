﻿using System.Threading.Tasks;
using com.csutil.algorithms.images;
using com.csutil.tests.AlgorithmTests.images;
using StbImageSharp;
using StbImageWriteSharp;
using Xunit;
using Zio;

namespace com.csutil.integrationTests.AlgorithmTests {

    public class ImageAlphaMattingTests {

        public ImageAlphaMattingTests(Xunit.Abstractions.ITestOutputHelper logger) { logger.UseAsLoggingOutput(); }

        [Fact]
        public async Task CutoutGenerationTest1() {
            var tempFolder = EnvironmentV2.instance.GetOrAddTempFolder("CutoutGenerationTest1");
            var inputImage = await MyImageFileRef.DownloadFileIfNeeded(tempFolder, "https://raw.githubusercontent.com/cs-util-com/cscore/master/CsCore/assets/16999.jpg");

            // Generate a cutout of the input image:
            var cutoutResult = GenerateCutOutAlgo.RunCutOutAlgo(inputImage, 240, 2, 20, 1e-5, 80, 10);

            var targetResultFile = tempFolder.GetChild("Cutout.png");
            await using var targetStream = targetResultFile.OpenOrCreateForReadWrite();
            var flipped = ImageUtility.FlipImageVertically(cutoutResult, inputImage.Width, inputImage.Height, (int)inputImage.ColorComponents);
            new ImageWriter().WritePng(flipped, inputImage.Width, inputImage.Height, StbImageWriteSharp.ColorComponents.RedGreenBlueAlpha, targetStream);
            Log.d("Cutout result saved to " + targetResultFile.GetFullFileSystemPath());
        }

        [Fact]
        public async Task TestGlobalMattingNormal() {
            var folder = EnvironmentV2.instance.GetOrAddTempFolder("TestGlobalMattingNormal");
            await TestGlobalMatting(folder);
        }

        [Fact]
        public async Task TestGlobalMattingFast() {
            var folder = EnvironmentV2.instance.GetOrAddTempFolder("TestGlobalMattingFast");
            await TestGlobalMatting(folder, iterationCount: 2, doOptionalExpansionOfKnownRegions: false, doRandomBoundaryPixelSampling: false);
        }

        /// <summary> Ported example usage from https://github.com/atilimcetin/global-matting/tree/master#code  </summary>
        private static async Task TestGlobalMatting(DirectoryEntry folder, int iterationCount = 10, bool doOptionalExpansionOfKnownRegions = true, int radius = 25, double eps = 1e-5, bool doRandomBoundaryPixelSampling = true) {
            var imageFile = folder.GetChild("GT04-image.png");
            var image = await MyImageFileRef.DownloadFileIfNeeded(imageFile, "https://raw.githubusercontent.com/cs-util/global-matting/master/GT04-image.png");

            var trimapFile = folder.GetChild("GT04-trimap.png");
            var trimap = await MyImageFileRef.DownloadFileIfNeeded(trimapFile, "https://raw.githubusercontent.com/cs-util/global-matting/master/GT04-trimap.png");

            var resultOfOriginalCppImplementation = folder.GetChild("GT04-alpha (result of original Cpp implementation).png");
            await MyImageFileRef.DownloadFileIfNeeded(resultOfOriginalCppImplementation, "https://raw.githubusercontent.com/cs-util/global-matting/master/GT04-alpha.png");

            var trimapBytes = trimap.Data;
            var imageMatting = new GlobalMatting(image, doRandomBoundaryPixelSampling, iterationCount);
            if (doOptionalExpansionOfKnownRegions) {
                imageMatting.ExpansionOfKnownRegions(ref trimapBytes, niter: 9);
            }
            imageMatting.RunGlobalMatting(trimapBytes, out var foreground, out var alphaData, out var conf);

            // filter the result with fast guided filter
            var alphaDataGuided = imageMatting.RunGuidedFilter(alphaData, radius, eps);

            var alpha = new ImageResult {
                Width = image.Width,
                Height = image.Height,
                SourceComponents = image.ColorComponents,
                ColorComponents = image.ColorComponents,
                BitsPerChannel = image.BitsPerChannel,
                Data = alphaDataGuided
            };

            for (int x = 0; x < trimap.Width; ++x) {
                for (int y = 0; y < trimap.Height; ++y) {
                    if (trimap.GetPixel(x, y).R == 0) {
                        alpha.SetPixel(x, y, new Pixel(0, 0, 0, 255));
                    } else if (trimap.GetPixel(x, y).R == 255) {
                        alpha.SetPixel(x, y, new Pixel(255, 255, 255, 255));
                    }
                }
            }

            var finalAlphaFile = folder.GetChild("FinalAlphaResult.png");
            await using var stream = finalAlphaFile.OpenOrCreateForReadWrite();
            var flipped = ImageUtility.FlipImageVertically(alpha.Data, image.Width, image.Height, (int)image.ColorComponents);
            new ImageWriter().WritePng(flipped, image.Width, image.Height, StbImageWriteSharp.ColorComponents.RedGreenBlueAlpha, stream);

            Log.d("See result in " + folder.GetFullFileSystemPath());
        }

    }

}