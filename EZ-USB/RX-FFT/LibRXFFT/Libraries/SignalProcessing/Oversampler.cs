﻿using System;

namespace LibRXFFT.Libraries.SignalProcessing
{
    public enum eOversamplingType
    {
        None,
        Linear,
        SinX
    }

    public class Oversampler
    {
        public int SinXDepth = 4;

        public readonly double Oversampling;
        private double HighVal = 1.0;
        private double LowVal = -1.0;
        public eOversamplingType Type = eOversamplingType.SinX;

        private double LastSampleValue = 0;
        private double[] LastBlockLastSamples = new double[0];
        private double[] NextLastBlockLastSamples = new double[0];
        private double[] NextBlockFirstSamples = new double[0];


        public Oversampler(double oversampling)
        {
            Oversampling = oversampling;
        }

        public Oversampler(double highVal, double lowVal)
        {
            HighVal = highVal;
            LowVal = lowVal;
        }

        public double[] Oversample(byte[] sourceData)
        {
            double[] outData = new double[(int)(sourceData.Length * 8 * Oversampling)];

            for (int outPos = 0; outPos < outData.Length; outPos++)
            {
                int bitPos = (int)(outPos / Oversampling);
                int bitNum = 7 - (bitPos % 8);
                int byteNum = bitPos / 8;

                double sampleVal;

                if (((sourceData[byteNum] >> bitNum) & 1) == 1)
                    sampleVal = HighVal;
                else
                    sampleVal = LowVal;

                outData[outPos] = sampleVal;
            }

            return outData;
        }

        public double[] Oversample(double[] srcData)
        {
            return Oversample(srcData, null);
        }

        public double[] Oversample(double[] srcData, double[] outData)
        {
            if (outData == null || outData.Length != (int)(srcData.Length * Oversampling))
            {
                outData = new double[(int)(srcData.Length * Oversampling)];
            }

            switch (Type)
            {
                case eOversamplingType.None:
                    for (int outPos = 0; outPos < outData.Length; outPos++)
                        outData[outPos] = srcData[(int)(outPos / Oversampling)];
                    break;

                case eOversamplingType.Linear:

                    double sampleValue1 = 0;
                    double sampleValue2 = 0;

                    for (int outPos = 0; outPos < outData.Length; outPos++)
                    {
                        double samplePos = outPos / Oversampling;
                        double delta = samplePos - Math.Floor(samplePos);

                        int samplePos2 = (int)Math.Max(0, Math.Min(srcData.Length - 1, samplePos));
                        int samplePos1 = samplePos2 - 1;

                        sampleValue2 = srcData[samplePos2];

                        if (samplePos1 < 0)
                            sampleValue1 = LastSampleValue;
                        else 
                            sampleValue1 = srcData[samplePos1];

                        outData[outPos] = sampleValue1 * (1 - delta) + sampleValue2 * delta;
                    }

                    LastSampleValue = sampleValue2;

                    break;

                case eOversamplingType.SinX:

                    if (LastBlockLastSamples.Length != SinXDepth)
                    {
                        LastBlockLastSamples = new double[SinXDepth];
                        NextLastBlockLastSamples = new double[SinXDepth];
                        NextBlockFirstSamples = new double[SinXDepth];
                    }

                    /* 
                     * save the last N samples for the next block etc
                     * 
                     * datablock:
                     * DDDDLLLFFF
                     * 
                     * |... DDDDD LLL| FFF
                     * L = NextLastBlockLastSamples
                     * F = NextBlockFirstSamples
                     * 
                     * next block:
                     * 
                     * LLL |FFF DDDDD ...|
                     */

                    /* save FFF into a tmp buffer, else they will get lost */
                    double[] tmpBuf = new double[SinXDepth];
                    Array.Copy(srcData, srcData.Length - SinXDepth, tmpBuf, 0, SinXDepth);

                    /* move DDDDD to the right... */
                    Array.Copy(srcData, 0, srcData, SinXDepth, srcData.Length - SinXDepth);
                    
                    /* ... and copy the last firstsamples FFFF into the first place... */
                    Array.Copy(NextBlockFirstSamples, 0, srcData, 0, SinXDepth);

                    /* now copy the FFF to the place they belong */
                    Array.Copy(tmpBuf, 0, NextBlockFirstSamples, 0, SinXDepth);

                    /* save LLL, which are now the last samples */
                    Array.Copy(srcData, srcData.Length - SinXDepth, NextLastBlockLastSamples, 0, SinXDepth);

                    for (int outPos = 0; outPos < outData.Length; outPos++)
                    {
                        int windowStart = (int)(outPos / Oversampling - SinXDepth);
                        int windowEnd = (int)(outPos / Oversampling + SinXDepth);

                        outData[outPos] = 0;

                        for (int windowPos = windowStart; windowPos < windowEnd; windowPos++)
                        {
                            double delta = outPos / Oversampling - windowPos;
                            double sampleValue = 0;

                            if (windowPos >= srcData.Length)
                            {
                                if(windowPos - srcData.Length >= 0 && windowPos - srcData.Length < NextBlockFirstSamples.Length)
                                    sampleValue = NextBlockFirstSamples[windowPos - srcData.Length];
                            }
                            else if (windowPos < 0)
                            {
                                if (windowPos + SinXDepth >= 0 && windowPos + SinXDepth < NextBlockFirstSamples.Length)
                                    sampleValue = NextBlockFirstSamples[windowPos + SinXDepth];
                            }
                            else
                            {
                                sampleValue = srcData[windowPos];
                            }

                            /* implement this as a lookup table? */
                            delta *= Math.PI;

                            if (delta != 0)
                                outData[outPos] += sampleValue * Math.Sin(delta)/delta;
                            else
                                outData[outPos] += sampleValue;
                        }
                    }

                    Array.Copy(NextLastBlockLastSamples, LastBlockLastSamples, SinXDepth);
                    break;

            }


            return outData;
        }

        public double[] Oversample(bool[] sourceData)
        {
            double[] outData = new double[(int)(sourceData.Length * Oversampling)];

            for (int outPos = 0; outPos < outData.Length; outPos++)
                outData[outPos] = sourceData[(int)(outPos / Oversampling)] ? 1.0f : -1.0f;

            return outData;
        }
    }
}
