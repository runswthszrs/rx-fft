﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LibRXFFT.Libraries.GMSK;

namespace LibRXFFT.Libraries.GSM
{
    public class SCHBurst : Burst
    {
        private bool DumpData = false;
        public const double Data1Bits = 39;
        public const double SyncBits = 64;
        public const double Data2Bits = 39;
        public const double SyncOffset = LeadingTailBits + Data1Bits;
        bool[] SCHData = new bool[78];
        bool[] SCHDataDecoded = new bool[39];

        public SCHBurst ()
        {
            Name = "SCH";
        }

        public override bool ParseData(GSMParameters param, bool[] decodedBurst)
        {
            Array.Copy(decodedBurst, 3, SCHData, 0, 39);
            Array.Copy(decodedBurst, 106, SCHData, 39, 39);

            bool[] data = ConvolutionalCoder.Decode(SCHData, SCHDataDecoded);
            if (data == null)
            {
                ErrorMessage = "(Error in ConvolutionalCoder)";
                param.Error = true;
                return false;
            }

            bool[] crc = CRC.Calc(data, 0, 35, CRC.PolynomialSCH);
            if (!CRC.Matches(crc))
            {
                ErrorMessage = "(Error in CRC)";
                param.Error = true;
                return false;
            }

            long BSIC = ByteUtil.BitsToLongRev(data, 2, 6);
            long T1 = ByteUtil.BitsToLongRev(data, new[] { new[] { 0, 2 }, new[] { 8, 8 }, new[] { 23, 1 } });
            long T2 = ByteUtil.BitsToLongRev(data, 18, 5);
            long T3M = ByteUtil.BitsToLongRev(data, new[] { new[] { 16, 2 }, new[] { 24, 1 } });
            long T3 = (10 * T3M) + 1;

            long FN;
            if (T2 < T3)
                FN = 51 * ((T3 - T2) % 26) + T3 + 51 * 26 * T1;
            else
                FN = 51 * (26 - ((T2 - T3) % 26)) + T3 + 51 * 26 * T1;

            param.AbsoluteFrameNumber = FN;
            param.CurrentControlFrame = T3;
            param.CurrentTrafficFrame = T2;
            param.CurrentTimeSlot = 0;

            if (DumpData)
            {
                StatusMessage = 
                    "BSIC: " + String.Format("{0,3}", BSIC) +
                    "  T1: " + String.Format("{0,5}", T1) +
                    "  T2: " + String.Format("{0,3}", T2) +
                    "  T3: " + String.Format("{0,3}", T3) +
                    "  FN: " + String.Format("{0,8}", FN) +
                    "  TrainOffs: " + String.Format("{0,3}", param.SampleOffset);
            }
            param.Error = false;

            return true;
        }
    }
}
