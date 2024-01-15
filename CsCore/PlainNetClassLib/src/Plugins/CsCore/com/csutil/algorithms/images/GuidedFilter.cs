using System;
using System.Collections.Generic;
using com.csutil.http.apis;

namespace com.csutil.algorithms.images {

    // TODO create a port of https://raw.githubusercontent.com/atilimcetin/guided-filter/master/guidedfilter.cpp 
    public class GuidedFilter {

        private byte[] image;
        private int width;
        private int height;
        private int colorComponents;
        private int r;
        private double eps;

        public GuidedFilter(byte[] image, int width, int height, int colorComponents, int r, double eps) {
            this.image = image;
            this.width = width;
            this.height = height;
            this.colorComponents = colorComponents;
            this.r = r;
            this.eps = eps;
        }
        public GuidedFilter init(byte[] image, int radius, double eps) {
            if(colorComponents == 1) {
                return new GuidedFilterMono(image, width, height, colorComponents, 2 * radius + 1, eps);
            }
            else if(colorComponents == 4) {
                return new GuidedFilterColor(image, width, height, colorComponents, 2 * radius + 1, eps);
            }
            throw new Exception("not correct channel image");
        }
        public abstract class GuidedFilterImpl {
            public static byte[] Filter(in byte[] image, int colorComponents, GuidedFilter gF) {
                var p2 = image;
                var channel = FindFirstByte(image);
                var result = new byte[image.Length];
                if (colorComponents == 1) {
                    result = ((GuidedFilterMono)gF).FilterSingleChannel(p2, channel);
                }
                else {
                    var pc = SplitChannels(image, colorComponents, gF);

                    for(int i = 0; i < pc.Count; ++i) {
                        pc[i] = ((GuidedFilterColor)gF).FilterSingleChannel(pc[i]);
                    }

                    result = ByteArrayAdd(ByteArrayAdd(pc[2], pc[3]), ByteArrayAdd(pc[0], pc[1])); //Only for 4 Channel Image!!!!
                }
                return result; 
            }
        }
        public class GuidedFilterMono : GuidedFilter {

            private byte[] channel;
            private byte[] mean1;
            private byte[] variance;
            
            public GuidedFilterMono(byte[] image, int width, int height, int colorComponents, int r, double eps) :
                base(image, width, height, colorComponents, r, eps) {

                mean1 = BoxFilter(image, r);
                var mean2 = BoxFilter(ByteArrayMult(image, image), r);
                variance = ByteArraySub(mean2, ByteArrayMult(mean1, mean1));
            }


            public byte[] FilterSingleChannel(byte[] imageSingleChannel, int currentChannel) {
                var numberImage = ConvertToDouble(imageSingleChannel);
                var mean_p = BoxFilter(imageSingleChannel, r);
                var mean_Ip = BoxFilter(ByteArrayMult(imageSingleChannel, mean_p), r);
                var cov_Ip = ByteArraySub(mean_Ip, ByteArrayMult(mean1, mean_p));

                var meanNumber = ConvertToDouble(mean_p);
                var covariance = ConvertToDouble(cov_Ip);
                var varianceNumber = ConvertToDouble(variance);
                var meanI = ConvertToDouble(mean1);
                
                // var varianceEps = new double[variance.Length];
                // for (var i = 0; i < variance.Length; i++) {
                //     if(i % colorComponents == currentChannel)
                //         varianceEps[i] = varianceNumber[i] + eps;
                // }
                var varianceEps = AddValueToSingleChannel(varianceNumber, eps, currentChannel);
                
                var a = DivideArrays(covariance, varianceEps);
                var b = SubArrays(meanNumber, MultArrays(a, meanI));

                var meanA = BoxFilter(ConvertToByte(a), r);
                var meanB = BoxFilter(ConvertToByte(b), r);

                return ByteArrayAdd(ByteArrayMult(meanA, image), meanB);
            }
        }


        public class GuidedFilterColor : GuidedFilter {
            private int iChannels;
            private double[] meanI_R, meanI_G, meanI_B;
            private double[] invRR, invRG, invRB, invGG, invGB, invBB;
            private double[] redImageDouble, greenImageDouble, blueImageDouble;
            private byte[] redImage, greenImage, blueImage;

            public GuidedFilterColor(byte[] image, int width, int height, int colorComponents, int r, double eps) :
                base(image, width, height, colorComponents, r, eps) {

                redImage = CreateSingleChannel(image, 0);
                greenImage = CreateSingleChannel(image, 1);
                blueImage = CreateSingleChannel(image, 2);
                redImageDouble = ConvertToDouble(redImage);
                greenImageDouble = ConvertToDouble(greenImage);
                blueImageDouble = ConvertToDouble(blueImage);    
                    
                meanI_R = ConvertToDouble(BoxFilter(redImage, r));
                meanI_G = ConvertToDouble(BoxFilter(greenImage, r));
                meanI_B = ConvertToDouble(BoxFilter(blueImage, r));
                
                
                // variance of I in each local patch: the matrix Sigma in Eqn (14).
                // Note the variance in each local patch is a 3x3 symmetric matrix:
                //           rr, rg, rb
                //   Sigma = rg, gg, gb
                //           rb, gb, bb
                
                //TODO Question - won't any combination such as rg or similar just be an empty image, as a 0 from one color channel 
                //TODO makes the product of two different channels full of 0s?
                var varRR_Eps = AddValueToSingleChannel(MultArrays(meanI_R, meanI_R), eps, 0);
                var varianceI_RR = SubArrays(ConvertToDouble(BoxFilter(ByteArrayMult(redImage, redImage), r)), varRR_Eps);
                
                var varRG_Eps = AddValueToSingleChannel(MultArrays(meanI_R, meanI_G), eps, 0);
                var varianceI_RG = SubArrays(ConvertToDouble(BoxFilter(ByteArrayMult(redImage, greenImage), r)), varRG_Eps);
                
                var varRB_Eps = AddValueToSingleChannel(MultArrays(meanI_R, meanI_B), eps, 0);
                var varianceI_RB = SubArrays(ConvertToDouble(BoxFilter(ByteArrayMult(redImage, blueImage), r)), varRB_Eps);
                
                var varGG_Eps = AddValueToSingleChannel(MultArrays(meanI_G, meanI_G), eps, 1);
                var varianceI_GG = SubArrays(ConvertToDouble(BoxFilter(ByteArrayMult(greenImage, greenImage), r)), varGG_Eps);
                
                var varGB_Eps = AddValueToSingleChannel(MultArrays(meanI_G, meanI_B), eps, 1);
                var varianceI_GB = SubArrays(ConvertToDouble(BoxFilter(ByteArrayMult(greenImage, blueImage), r)), varGB_Eps);
                
                var varBB_Eps = AddValueToSingleChannel(MultArrays(meanI_G, meanI_G), eps, 2);
                var varianceI_BB = SubArrays(ConvertToDouble(BoxFilter(ByteArrayMult(blueImage, blueImage), r)), varBB_Eps);
                
                // Inverse of Sigma + Eps + I
                invRR = SubArrays(MultArrays(varianceI_GG, varianceI_BB), MultArrays(varianceI_GB, varianceI_GB));
                invRG = SubArrays(MultArrays(varianceI_GB, varianceI_RB), MultArrays(varianceI_RG, varianceI_BB));
                invRB = SubArrays(MultArrays(varianceI_RG, varianceI_GB), MultArrays(varianceI_GG, varianceI_RB));
                invGG = SubArrays(MultArrays(varianceI_RR, varianceI_BB), MultArrays(varianceI_RB, varianceI_RB));
                invGB = SubArrays(MultArrays(varianceI_RB, varianceI_RG), MultArrays(varianceI_RR, varianceI_GB));
                invBB = SubArrays(MultArrays(varianceI_RR, varianceI_GG), MultArrays(varianceI_RG, varianceI_RG));

                var covDet = AddArrays(AddArrays(MultArrays(invRR, varianceI_RR), MultArrays(invRG, varianceI_RG)),MultArrays(invRB, varianceI_RB));

                invRR = DivideArrays(invRR, covDet);
                invRG = DivideArrays(invRG, covDet);
                invRB = DivideArrays(invRB, covDet);
                invGG = DivideArrays(invGG, covDet);
                invGB = DivideArrays(invGB, covDet);
                invBB = DivideArrays(invBB, covDet);
            }


            public byte[] FilterSingleChannel(byte[] p) {
                var meanI = BoxFilter(p, r);
                var meanIp_R = BoxFilter(ByteArrayMult(redImage, p), r);
                var meanIp_G = BoxFilter(ByteArrayMult(greenImage, p), r);
                var meanIp_B = BoxFilter(ByteArrayMult(blueImage, p), r);
                
                var cov_Ip_R = SubArrays(ConvertToDouble(meanIp_R), MultArrays(ConvertToDouble(meanIp_R), ConvertToDouble(meanI)));
                var cov_Ip_G = SubArrays(ConvertToDouble(meanIp_G), MultArrays(ConvertToDouble(meanIp_G), ConvertToDouble(meanI)));
                var cov_Ip_B = SubArrays(ConvertToDouble(meanIp_B), MultArrays(ConvertToDouble(meanIp_B), ConvertToDouble(meanI)));

                var a_r = AddArrays(AddArrays(MultArrays(invRR, cov_Ip_R), MultArrays(invRG, cov_Ip_G)), MultArrays(invRB, cov_Ip_B));
                var a_g = AddArrays(AddArrays(MultArrays(invRG, cov_Ip_R), MultArrays(invGG, cov_Ip_G)), MultArrays(invGB, cov_Ip_B));
                var a_b = AddArrays(AddArrays(MultArrays(invRB, cov_Ip_R), MultArrays(invGB, cov_Ip_G)), MultArrays(invBB, cov_Ip_B));

                var b1 = SubArrays(ConvertToDouble(meanI), MultArrays(a_r, meanI_R));
                var b2 = MultArrays(a_g, meanI_G);
                var b3 = MultArrays(a_b, meanI_B);
                var b = SubArrays(SubArrays(b1, b2), b3);

                var a_R_byte = ConvertToByte(a_r);
                var a_G_byte = ConvertToByte(a_g);
                var a_B_byte = ConvertToByte(a_b);

                var res1 = ByteArrayMult(BoxFilter(a_R_byte, r), redImage);
                var res2 = ByteArrayMult(BoxFilter(a_G_byte, r), greenImage);
                var res3 = ByteArrayMult(BoxFilter(a_B_byte, r), blueImage);
                var res4 = BoxFilter(ConvertToByte(b), r);
                return ByteArrayAdd(ByteArrayAdd(ByteArrayAdd(res1, res2), res3), res4);
            }
        }


        
        
        public static byte[] RunGuidedFilter(byte[] bytes, byte[] alpha, int i, double eps) {
            throw new NotImplementedException("TODO");
        }


        private byte[] BoxFilter(byte[] image, int boxSize) {
            return ImageBlur.RunBoxBlur(image, width, height, boxSize, colorComponents);
        }


        
        
        public static byte[] ByteArrayMult(byte[] image1, byte[] image2) {
            if (image1.Length != image2.Length) throw new ArgumentException("Input arrays must have the same length.");
            var result = new byte[image1.Length];
            for (var i = 0; i < image1.Length; i++) {
                var intResult = (int)image1[i] * image2[i];
                result[i] = (byte)Math.Min(byte.MaxValue, Math.Max(0, intResult));
            }
            return result;
        }
        
        public static byte[] ByteArraySub(byte[] image1, byte[] image2) {
            if (image1.Length != image2.Length) { throw new ArgumentException("Arrays must have the same length"); }

            var length = image1.Length;
            var result = new byte[length];

            for (int i = 0; i < length; i++) {
                // Perform element-wise subtraction with underflow check
                var subtractionResult = image1[i] - image2[i];

                if (subtractionResult < 0) {
                    // Underflow occurred, set result to 0
                    result[i] = 0;
                } else {
                    result[i] = (byte)subtractionResult;
                }
            }

            return result;
        }

        private static byte[] ByteArrayAdd(byte[] image1, byte[] image2) {
            if (image1.Length != image2.Length) { throw new ArgumentException("Arrays must have the same length"); }

            var length = image1.Length;
            var result = new byte[length];

            for (int i = 0; i < length; i++) {
                // Perform element-wise subtraction with underflow check
                var additionResult = image1[i] + image2[i];

                if (additionResult > 255) {
                    // Underflow occurred, set result to 0
                    result[i] = 255;
                } else {
                    result[i] = (byte)additionResult;
                }
            }

            return result;
        }
        
        private double[] AddValueToSingleChannel(double[] array, double eps, int channel) {
            var varianceEps = new double[array.Length];
            for (var i = 0; i < array.Length; i++) {
                if (channel == 3)
                    varianceEps[i] = array[i];
                else if (i % colorComponents == channel)
                    varianceEps[i] = array[i] + eps;
            }
            return varianceEps;
        }

        private static double[] MultArrays(double[] array1, double[] array2)
        {
            if (array1 == null || array2 == null) { throw new ArgumentException("Input arrays cannot be null."); }
            if (array1.Length != array2.Length) { throw new ArgumentException("Input arrays must have the same length."); }

            var result = new double[array1.Length];
            for (var i = 0; i < array1.Length; i++){
                result[i] = array1[i] * array2[i];
            }

            return result;
        }
        
        private static double[] DivideArrays(double[] array1, double[] array2) {
            if (array1.Length != array2.Length) throw new ArgumentException("Input arrays must have the same length.");
            var res = new double[array1.Length];
            for (var i = 0; i < array1.Length; i++) {
                res[i] = array1[i] / array2[i];
            }
            return res;
        }
        
        private static double[] SubArrays(double[] array1, double[] array2)
        {
            if (array1 == null || array2 == null) { throw new ArgumentException("Input arrays cannot be null."); }
            if (array1.Length != array2.Length) { throw new ArgumentException("Input arrays must have the same length."); }

            var result = new double[array1.Length];
            for (var i = 0; i < array1.Length; i++){
                result[i] = array1[i] - array2[i];
            }
            return result;
        }
        
        private static double[] AddArrays(double[] array1, double[] array2)
        {
            if (array1 == null || array2 == null) { throw new ArgumentException("Input arrays cannot be null."); }
            if (array1.Length != array2.Length) { throw new ArgumentException("Input arrays must have the same length."); }

            var result = new double[array1.Length];
            for (var i = 0; i < array1.Length; i++){
                result[i] = array1[i] - array2[i];
            }
            return result;
        }

        public static double[] ConvertToDouble(byte[] data) {
            var res = new double[data.Length];
            for (var i = 0; i < data.Length; i++) {
                res[i] = data[i];
            }
            return res;
        }

        public static byte[] ConvertToByte(double[] data) {
            var res = new byte[data.Length];
            for (var i = 0; i < data.Length; i++) {
                res[i] = (byte)data[i];
            }
            return res;
        }

        public  byte[] CreateSingleChannel(byte[] image, int channel) {
            var newIm = new byte[image.Length];
            for (var i = 0; i < image.Length; i++) {
                if (i % colorComponents == channel) {
                    newIm[i] = image[i];
                } else if(i % colorComponents == 3 && colorComponents == 4){
                    newIm[i] = 255;
                } else {
                    newIm[i] = 0;
                }
            }
            return newIm;
        }
        private static int FindFirstByte(byte[] image) {
            for (int i = 0; i <= 3; i++) {
                if (image[i] != 0) { return i; } //channel range from 0 to 3 in rgba order 
            }
            return -1;
        }

        public static List<byte[]> SplitChannels(byte[] inputImage, int numberOfChannels, GuidedFilter gf) {
            var result = new List<byte[]>();
            for(int i = 0; i < numberOfChannels;i++) {
                result.Add(gf.CreateSingleChannel(inputImage, i));
            }
            return result;
        }


    }
}
