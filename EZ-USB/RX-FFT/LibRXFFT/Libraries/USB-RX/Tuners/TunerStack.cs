﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LibRXFFT.Libraries.USB_RX.Tuners
{
    public class TunerStack : DigitalTuner
    {
        private Tuner SlaveTuner;
        private Tuner MasterTuner;
        private long MasterTunerIFFreq;
        private long MasterTunerFreqSteps;
        private long CurrentFrequency;


        public TunerStack(Tuner masterTuner, DigitalTuner slaveTuner,
                long masterTunerIFFreq, long masterTunerFreqSteps)
            : this(masterTuner, (Tuner)slaveTuner, masterTunerIFFreq, masterTunerFreqSteps)
        {
            /* this is a digital tuner, register for sampling rate change */
            slaveTuner.SamplingRateChanged += new EventHandler(Tuner_SamplingRateChanged);
        }

        public TunerStack(Tuner masterTuner, Tuner slaveTuner,
                long masterTunerIFFreq, long masterTunerFreqSteps)
        {
            this.SlaveTuner = slaveTuner;
            this.MasterTuner = masterTuner;
            this.MasterTunerIFFreq = masterTunerIFFreq;
            this.MasterTunerFreqSteps = masterTunerFreqSteps;

            /* register for any filter width etc change */
            SlaveTuner.InvertedSpectrumChanged += new EventHandler(Tuner_InvertedSpectrumChanged);
            SlaveTuner.FrequencyChanged += new EventHandler(Tuner_FrequencyChanged);
            SlaveTuner.FilterWidthChanged += new EventHandler(Tuner_FilterWidthChanged);

            MasterTuner.FilterWidthChanged += new EventHandler(Tuner_FilterWidthChanged);
            MasterTuner.InvertedSpectrumChanged += new EventHandler(Tuner_InvertedSpectrumChanged);
            MasterTuner.FrequencyChanged += new EventHandler(Tuner_FrequencyChanged);
        }

        void Tuner_SamplingRateChanged(object sender, EventArgs e)
        {
            if (SamplingRateChanged != null)
                SamplingRateChanged(this, null);
        }

        void Tuner_FrequencyChanged(object sender, EventArgs e)
        {
            if (FrequencyChanged != null)
                FrequencyChanged(this, null);
        }

        void Tuner_InvertedSpectrumChanged(object sender, EventArgs e)
        {
            if (InvertedSpectrumChanged != null)
                InvertedSpectrumChanged(this, null);
        }

        void Tuner_FilterWidthChanged(object sender, EventArgs e)
        {
            if (FilterWidthChanged != null)
                FilterWidthChanged(this, null);
        }


        #region DigitalTuner Member

        public event EventHandler SamplingRateChanged;

        public long SamplingRate
        {
            get
            {
                if (SlaveTuner.GetType().IsAssignableFrom(typeof(DigitalTuner)))
                    throw new NotSupportedException();

                return ((DigitalTuner)SlaveTuner).SamplingRate;
            }
        }

        #endregion

        #region Tuner Members

        public event EventHandler FrequencyChanged;
        public event EventHandler InvertedSpectrumChanged;
        public event EventHandler FilterWidthChanged;

        public long LowestFrequency
        {
            get { return MasterTuner.LowestFrequency; }
        }

        public long HighestFrequency
        {
            get { return MasterTuner.HighestFrequency; }
        }

        public long UpperFilterMargin
        {
            get { return CurrentFrequency + FilterWidth / 2; }
        }

        public long LowerFilterMargin
        {
            get { return CurrentFrequency - FilterWidth / 2; }
        }

        public long FilterWidth
        {
            get
            {
                /* return the most narrow filter */
                return Math.Min(SlaveTuner.FilterWidth, MasterTuner.FilterWidth);
            }
        }

        public long GetFrequency()
        {
            long frequency;
            long freqSlave = this.SlaveTuner.GetFrequency();
            long freqMaster = this.MasterTuner.GetFrequency();

            if (InvertedSpectrum)
                frequency = MasterTunerIFFreq - freqSlave + freqMaster;
            else
                frequency = MasterTunerIFFreq - freqSlave - freqMaster;

            CurrentFrequency = frequency;

            return frequency;
        }

        public bool SetFrequency(long frequency)
        {
            long freqMaster = (frequency / MasterTunerFreqSteps) * MasterTunerFreqSteps;
            long freqSlave;

            if (InvertedSpectrum)
                freqSlave = MasterTunerIFFreq - (frequency - freqMaster);
            else
                freqSlave = MasterTunerIFFreq + (frequency - freqMaster);

            if (!SlaveTuner.SetFrequency(freqSlave))
                return false;
            if (!MasterTuner.SetFrequency(freqMaster))
                return false;

            CurrentFrequency = frequency;

            return true;
        }

        public bool InvertedSpectrum
        {
            get
            {
                return MasterTuner.InvertedSpectrum != SlaveTuner.InvertedSpectrum;
            }
        }

        #endregion
    }
}
