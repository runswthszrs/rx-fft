﻿using System.Threading;
using System.Windows.Forms;
using LibRXFFT.Libraries;
using LibRXFFT.Libraries.FFTW;
using System;
using LibRXFFT.Libraries.GSM.Misc;
using System.Drawing;
using System.Collections.Generic;

namespace LibRXFFT.Components.DirectX
{
    public partial class DirectXWaterfallFFTDisplay : UserControl
    {
        public UserEventCallbackDelegate UserEventCallback;

        private Color MarkerColor = Color.LightGreen;

        private Mutex FFTLock = new Mutex();
        private FFTTransformer FFT = new FFTTransformer(256);
        private int _FFTSize = 256;
        private double[] FFTResult = new double[256];

        private bool LevelBarActive = false;
        private LabelledLine LevelBarUpper = new LabelledLine("Waterfall upper limit", 0, Color.BlueViolet);
        private LabelledLine LevelBarLower = new LabelledLine("Waterfall lower limit", 0, Color.BlueViolet);


        private double IMax = -100;
        private double IMin = 100;

        public DirectXWaterfallFFTDisplay()
        {
            InitializeComponent();

            /* we work with already squared FFT values for performance reasons */
            FFTDisplay.SquaredFFTData = true;
            WaterfallDisplay.SquaredFFTData = true;

            /* handle X Zoom and X Offset ourselves */
            FFTDisplay.UserEventCallback = UserEventCallbackFunc;
            WaterfallDisplay.UserEventCallback = UserEventCallbackFunc;

            FFTDisplay.EventActions[eUserEvent.MouseDoubleClickLeft] = eUserAction.UserCallback;
            FFTDisplay.EventActions[eUserEvent.MouseClickRight] = eUserAction.UserCallback;
            FFTDisplay.EventActions[eUserEvent.MousePosX] = eUserAction.UserCallback;
            FFTDisplay.EventActions[eUserEvent.MouseDragXControl] = eUserAction.UserCallback;
            FFTDisplay.EventActions[eUserEvent.MouseDragY] = eUserAction.YOffset;
            FFTDisplay.EventActions[eUserEvent.MouseDragXShift] = eUserAction.UserCallback;
            FFTDisplay.EventActions[eUserEvent.MouseDragYShift] = eUserAction.YOffset;
            FFTDisplay.EventActions[eUserEvent.MouseWheelUp] = eUserAction.UserCallback;
            FFTDisplay.EventActions[eUserEvent.MouseWheelDown] = eUserAction.UserCallback;
            FFTDisplay.EventActions[eUserEvent.MouseWheelUpShift] = eUserAction.UserCallback;
            FFTDisplay.EventActions[eUserEvent.MouseWheelDownShift] = eUserAction.UserCallback;

            WaterfallDisplay.EventActions[eUserEvent.MouseDoubleClickLeft] = eUserAction.UserCallback;
            WaterfallDisplay.EventActions[eUserEvent.MouseClickRight] = eUserAction.UserCallback;
            WaterfallDisplay.EventActions[eUserEvent.MousePosX] = eUserAction.UserCallback;
            WaterfallDisplay.EventActions[eUserEvent.MouseDragX] = eUserAction.UserCallback;
            WaterfallDisplay.EventActions[eUserEvent.MouseDragXShift] = eUserAction.UserCallback;
            WaterfallDisplay.EventActions[eUserEvent.MouseWheelUpAlt] = eUserAction.UserCallback;
            WaterfallDisplay.EventActions[eUserEvent.MouseWheelDownAlt] = eUserAction.UserCallback;
        }

        /* return a value between -0.5 to 0.5 that indicates the cursor position relative to the center */
        public double RelativeCursorXPos
        {
            get
            {
                return FFTDisplay.XRelativeCoordFromCursorPos();
            }
        }

        public long FrequencyFromCursorPosOffset(double xOffset)
        {
            return FFTDisplay.FrequencyFromCursorPosOffset(xOffset);
        }

        public double SamplingRate
        {
            set 
            { 
                FFTDisplay.SamplingRate = value;
                WaterfallDisplay.SamplingRate = value;
            }
            get { return FFTDisplay.SamplingRate; }
        }


        public double UpdateRate
        {
            set 
            { 
                FFTDisplay.UpdateRate = value;
                WaterfallDisplay.UpdateRate = value;
            }
            get { return FFTDisplay.UpdateRate; }
        }

        public long SamplesToAverage
        {
            set 
            {
                FFTDisplay.SamplesToAverage = value;
                WaterfallDisplay.SamplesToAverage = value;
            }
            get { return FFTDisplay.SamplesToAverage; }
        }

        public bool SavingEnabled
        {
            get { return WaterfallDisplay.SavingEnabled; }
            set { WaterfallDisplay.SavingEnabled = value; }
        }

        public string SavingName
        {
            get { return WaterfallDisplay.SavingName; }
            set { WaterfallDisplay.SavingName = value; }
        }

        public double Averaging
        {
            get { return FFTDisplay.Averaging; }
            set 
            { 
                FFTDisplay.Averaging = value;
                WaterfallDisplay.Averaging = value;
            }
        }

        public double CenterFrequency
        {
            get
            {
                return FFTDisplay.CenterFrequency;
            }
            set
            {
                FFTDisplay.CenterFrequency = value;
                WaterfallDisplay.CenterFrequency = value;
            }
        }


        public long Frequency
        {
            get
            {
                return FFTDisplay.FrequencyFromCursorPos();
            }
        }

        public FFTTransformer.eWindowingFunction WindowingFunction
        {
            get { return FFT.WindowingFunction; }
            set { FFT.WindowingFunction = value; }
        }

        public void UserEventCallbackFunc(eUserEvent evt, double param)
        {
            if (UserEventCallback != null)
                UserEventCallback(evt, param);

            switch (evt)
            {
                case eUserEvent.StatusUpdated:
                    if (!WaterfallDisplay.LevelBarActive && LevelBarActive)
                    {
                        FFTDisplay.LabelledHorLines.Remove(LevelBarLower);
                        FFTDisplay.LabelledHorLines.Remove(LevelBarUpper);

                        FFTDisplay.LabelledVertLines.AddLast(new LabelledLine("Test", 100000000, Color.Green));
                        LevelBarActive = false;
                    }

                    if (WaterfallDisplay.LevelBarActive && !LevelBarActive)
                    {
                        FFTDisplay.LabelledHorLines.AddLast(LevelBarLower);
                        FFTDisplay.LabelledHorLines.AddLast(LevelBarUpper);
                        LevelBarActive = true;
                    }

                    LevelBarLower.Position = WaterfallDisplay.LeveldBBlack;
                    LevelBarUpper.Position = WaterfallDisplay.LeveldBWhite;
                    FFTDisplay.UpdateOverlays = true;
                    break;

                case eUserEvent.MouseClickRight:
                    break;

                case eUserEvent.MouseWheelUp:
                    FFTDisplay.ProcessUserAction(eUserAction.YZoomIn, param);
                    break;

                case eUserEvent.MouseWheelDown:
                    FFTDisplay.ProcessUserAction(eUserAction.YZoomOut, param);
                    break;


                case eUserEvent.MousePosX:
                    FFTDisplay.ProcessUserAction(eUserAction.XPos, param);
                    WaterfallDisplay.ProcessUserAction(eUserAction.XPos, param);
                    break;

                case eUserEvent.MouseDragXShift:
                    FFTDisplay.ProcessUserAction(eUserAction.XOffsetOverview, param);
                    WaterfallDisplay.ProcessUserAction(eUserAction.XOffsetOverview, param);
                    break;

                case eUserEvent.MouseDragX:
                    FFTDisplay.ProcessUserAction(eUserAction.XOffset, param);
                    WaterfallDisplay.ProcessUserAction(eUserAction.XOffset, param);
                    break;

                case eUserEvent.MouseWheelUpShift:
                    FFTDisplay.ProcessUserAction(eUserAction.XZoomIn, param);
                    WaterfallDisplay.ProcessUserAction(eUserAction.XZoomIn, param);
                    break;

                case eUserEvent.MouseWheelDownShift:
                    FFTDisplay.ProcessUserAction(eUserAction.XZoomOut, param);
                    WaterfallDisplay.ProcessUserAction(eUserAction.XZoomOut, param);
                    break;

                case eUserEvent.MouseWheelUpAlt:
                    if (WaterfallDisplay.UpdateRate < 512)
                    {
                        FFTDisplay.UpdateRate *= 2;
                        WaterfallDisplay.UpdateRate *= 2;
                    }
                    break;

                case eUserEvent.MouseWheelDownAlt:
                    if (WaterfallDisplay.UpdateRate > 1)
                    {
                        FFTDisplay.UpdateRate /= 2;
                        WaterfallDisplay.UpdateRate /= 2;
                    }
                    break;
            }
        }

        public int FFTSize
        {
            get { return _FFTSize; }
            set
            {
                lock (FFTLock)
                {
                    _FFTSize = value;
                    FFTResult = new double[_FFTSize];
                    FFT = new FFTTransformer(value);

                    FFTDisplay.FFTSize = value;
                    WaterfallDisplay.FFTSize = value;
                }
            }
        }

        public void ProcessData(byte[] dataBuffer)
        {
            const int bytePerSample = 2;
            const int channels = 2;

            if (FFTDisplay.EnoughData)
                return;

            lock (FFTLock)
            {
                int samplePairs = dataBuffer.Length / (channels * bytePerSample);

                for (int samplePair = 0; samplePair < samplePairs; samplePair++)
                {
                    int samplePairPos = samplePair * bytePerSample * channels;
                    double I = ByteUtil.getDoubleFromBytes(dataBuffer, samplePairPos);
                    double Q = ByteUtil.getDoubleFromBytes(dataBuffer, samplePairPos + bytePerSample);


                    FFT.AddSample(I, Q);

                    if (FFT.ResultAvailable)
                    {
                        FFT.GetResultSquared(FFTResult);

                        FFTDisplay.ProcessFFTData(FFTResult);
                        WaterfallDisplay.ProcessFFTData(FFTResult);
                    }
                }
            }
        }

        public void ProcessData(double[] iSamples, double[] qSamples)
        {
            if (FFTDisplay.EnoughData)
                return;

            lock (FFTLock)
            {
                int samplePairs = iSamples.Length;

                for (int samplePair = 0; samplePair < samplePairs; samplePair++)
                {
                    double I = iSamples[samplePair];
                    double Q = qSamples[samplePair];

                    FFT.AddSample(I, Q);

                    if (FFT.ResultAvailable)
                    {
                        FFT.GetResultSquared(FFTResult);

                        FFTDisplay.ProcessFFTData(FFTResult);
                        WaterfallDisplay.ProcessFFTData(FFTResult);
                    }
                }
            }
        }


        public void ProcessSample(double I, double Q)
        {
            lock (FFTLock)
            {
                FFT.AddSample(I, Q);

                if (FFT.ResultAvailable)
                {
                    FFT.GetResultSquared(FFTResult);

                    FFTDisplay.ProcessFFTData(FFTResult);
                    WaterfallDisplay.ProcessFFTData(FFTResult);
                }
            }
        }

        public LinkedList<FrequencyMarker> Markers
        {
            set
            {
                FFTDisplay.LabelledVertLines.Clear();

                foreach (FrequencyMarker marker in value)
                {
                    FFTDisplay.LabelledVertLines.AddLast(new LabelledLine(marker.Label, marker.Frequency, MarkerColor));
                }

                FFTDisplay.UpdateOverlays = true;
            }
        }
    }
}