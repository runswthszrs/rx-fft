﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using LibRXFFT.Components.GDI;
using LibRXFFT.Libraries;
using LibRXFFT.Libraries.GMSK;
using LibRXFFT.Libraries.GSM.Bursts;
using LibRXFFT.Libraries.GSM.Layer1;
using LibRXFFT.Libraries.GSM.Layer2;
using LibRXFFT.Libraries.GSM.Layer3;
using LibRXFFT.Libraries.SampleSources;
using LibRXFFT.Libraries.ShmemChain;
using LibRXFFT.Libraries.SignalProcessing;
using System.Text;
using LibRXFFT.Components.DirectX;

namespace GSM_Analyzer
{
    public partial class GSMAnalyzer : Form
    {
        public SampleSource Source;

        private Thread ReadThread;
        private bool ThreadActive;

        public double[] BurstLengthJitter = new[] { 0.0d, 0.0d, 0.0d, 0.0d };

        private Semaphore SingleStepSem = new Semaphore(0, 1, "SingleStepSemaphore");
        private bool SingleStep;

        internal TimeSlotHandler Handler;
        internal GSMParameters Parameters;
        private FilterDialog FilterWindow;
        private BurstVisualizer BurstWindow;
        private OptionsDialog OptionsWindow;

        private Object BurstWindowLock = new Object();

        private DateTime LastTextBoxUpdate = DateTime.Now;
        private StringBuilder TextBoxBuffer = new StringBuilder(32768);
        private System.Windows.Forms.Timer TextBoxCommitTimer = new System.Windows.Forms.Timer();

        public bool Subsampling = true;
        public int InternalOversampling = 1;
        public int SubSampleOffset = 0;

        internal double DefaultSamplingRate = 2184533;
        private double BT = 0.3d;


        internal double CurrentSampleRate
        {
            get
            {
                if (Source != null)
                    return Source.OutputSamplingRate;

                return DefaultSamplingRate;
            }
        }

        internal double Oversampling
        {
            get
            {
                return CurrentSampleRate / 270833;
            }
        }


        public GSMAnalyzer()
        {
            InitializeComponent();
            Parameters = new GSMParameters();

            /* already init here to load XML files */
            InitTimeSlotHandler();

            TextBoxCommitTimer.Tick += new EventHandler(TextBoxCommitTimer_Tick);
            TextBoxCommitTimer.Interval = 100;
        }

        void TextBoxCommitTimer_Tick(object sender, EventArgs e)
        {
            /* just call the AddMessage routine to commit the text in buffers */
            AddMessage("");
        }

        public void RegisterTriggers(L3Handler L3Handler)
        {
            L3Handler.PDUDataTriggers.Add("LAIUpdate", TriggerLAIUpdate);
        }

        private void TriggerLAIUpdate(L3Handler L3Handler)
        {
            string mccMncString = "-1";
            string lacString = "-1";
            string cellIdentString = "-1";

            lock (L3Handler.PDUDataFields)
            {
                if (L3Handler.PDUDataFields.ContainsKey("MCC/MNC"))
                    mccMncString = L3Handler.PDUDataFields["MCC/MNC"];

                if (L3Handler.PDUDataFields.ContainsKey("LAC"))
                    lacString = L3Handler.PDUDataFields["LAC"];

                if (L3Handler.PDUDataFields.ContainsKey("CellIdent"))
                    cellIdentString = L3Handler.PDUDataFields["CellIdent"];
            }

            int mnc = -1;
            int mcc = -1;
            int lac = -1;
            int cellIdent = -1;

            if (mccMncString.Length == 6)
            {
                int.TryParse(mccMncString.Substring(0, 3), out mcc);
                int.TryParse(mccMncString.Substring(3, 3), out mnc);
            }

            int.TryParse(lacString, out lac);
            int.TryParse(cellIdentString, out cellIdent);

            UpdateCellInfo(mcc, mnc, lac, cellIdent, Parameters.CBCH);
        }


        public void AddMessage(String msg)
        {
            try
            {
                this.BeginInvoke(new addMessageDelegate(AddMessageFunc), new object[] { msg });
            }
            catch (Exception)
            {
            }
        }

        public void UpdateCellInfo(int mcc, int mnc, long lac, long cellIdent, eTriState hasCBCH)
        {
            try
            {
                this.BeginInvoke(new updateCellInfoDelegate(UpdateCellInfoFunc), new object[] { mcc, mnc, lac, cellIdent, hasCBCH });
            }
            catch (Exception)
            {
            }
        }

        private void UpdateCellInfoFunc(int mcc, int mnc, long lac, long cellident, eTriState hasCBCH)
        {
            if (mcc > 0 && mnc > 0)
                lblMCCMNC.Text = "" + string.Format("{0:000}", mcc) + " " + string.Format("{0:000}", mnc);
            else
                lblMCCMNC.Text = "---";

            if (lac > 0)
                lblLAC.Text = "" + lac;
            else
                lblLAC.Text = "---";

            if (cellident > 0)
                lblCellIdent.Text = "" + cellident;
            else
                lblCellIdent.Text = "---";

            if (hasCBCH == eTriState.Yes)
                lblCellBroadcast.Text = "yes";
            else if (hasCBCH == eTriState.No)
                lblCellBroadcast.Text = "no";
            else
                lblCellBroadcast.Text = "---";
        }

        delegate void addMessageDelegate(String msg);
        delegate void updateErrorSuccessDelegate(long err, long succ, long TN, long T1, long T2, long T3);
        delegate void updateFreqOffsetDelegate(double offset);
        delegate void updateCellInfoDelegate(int mcc, int mnc, long lac, long cellIdent, eTriState hasCBCH);

        public void AddMessageFunc(String msg)
        {
            if (msg == null)
            {
                txtLog.Clear();
                TextBoxBuffer.Length = 0;
                return;
            }

            if (DateTime.Now.Subtract(LastTextBoxUpdate).TotalMilliseconds < 100)
            {
                TextBoxBuffer.Append(msg);
                TextBoxCommitTimer.Start();
            }
            else
            {
                TextBoxCommitTimer.Stop();
                if (TextBoxBuffer.Length > 0)
                {
                    txtLog.AppendText(TextBoxBuffer.ToString());
                    TextBoxBuffer.Length = 0;
                }
                txtLog.AppendText(msg);
                LastTextBoxUpdate = DateTime.Now;
            }
        }


        void UpdateFreqOffset(double offset)
        {
            string[] scale = { "", "k", "G", "T" };
            int fact = 0;

            while (Math.Abs(offset) > 1000)
            {
                offset /= 1000;
                fact++;
            }
            lblFreqOffset.Text = String.Format("{0:0.00}", offset) + " " + scale[fact] + "Hz";

        }

        void UpdateUIStatus(GSMParameters param)
        {
            switch (param.State)
            {
                case eGSMState.Idle:
                case eGSMState.Reset:
                    statusSearch.State = eLampState.Grayed;
                    statusTrain.State = eLampState.Grayed;
                    statusLock.State = eLampState.Grayed;

                    lock (Handler.L3.PDUDataFields)
                    {
                        Handler.L3.PDUDataFields.Remove("MCC/MNC");
                        Handler.L3.PDUDataFields.Remove("LAC");
                        Handler.L3.PDUDataFields.Remove("CellIdent");
                    }
                    param.CBCH = eTriState.Unknown;
                    UpdateCellInfo(-1, -1, -1, -1, param.CBCH);

                    break;

                case eGSMState.FCCHSearch:
                    statusSearch.State = eLampState.Green;
                    statusTrain.State = eLampState.Grayed;
                    statusLock.State = eLampState.Grayed;

                    lock (Handler.L3.PDUDataFields)
                    {
                        Handler.L3.PDUDataFields.Remove("MCC/MNC");
                        Handler.L3.PDUDataFields.Remove("LAC");
                        Handler.L3.PDUDataFields.Remove("CellIdent");
                    }
                    param.CBCH = eTriState.Unknown;
                    UpdateCellInfo(-1, -1, -1, -1, param.CBCH);
                    break;

                case eGSMState.SCHSearch:
                    statusSearch.State = eLampState.Green;
                    statusTrain.State = eLampState.Green;
                    statusLock.State = eLampState.Grayed;

                    lock (Handler.L3.PDUDataFields)
                    {
                        Handler.L3.PDUDataFields.Remove("MCC/MNC");
                        Handler.L3.PDUDataFields.Remove("LAC");
                        Handler.L3.PDUDataFields.Remove("CellIdent");
                    }
                    param.CBCH = eTriState.Unknown;
                    UpdateCellInfo(-1, -1, -1, -1, param.CBCH);
                    break;

                case eGSMState.Lock:
                    statusSearch.State = eLampState.Green;
                    statusTrain.State = eLampState.Green;
                    statusLock.State = param.Errors > 0 ? eLampState.Red : eLampState.Green;
                    break;
            }
        }

        /* intentionally dont pass GSMParameters due to thread safeness */
        void UpdateErrorSuccess(long err, long succ, long TN, long T1, long T2, long T3)
        {
            if (err >= 0)
                lblErrors.Text = "" + err;
            else
                lblErrors.Text = "---";

            if (succ >= 0)
                lblSucess.Text = "" + succ;
            else
                lblSucess.Text = "---";

            if (err + succ > 0)
                lblRate.Text = "" + ((succ * 100) / (succ + err)) + " %";
            else
                lblRate.Text = "--- %";

            if (T1 >= 0)
                lblT1.Text = "" + T1;
            else
                lblT1.Text = "---";

            if (T2 >= 0)
                lblT2.Text = "" + T2;
            else
                lblT2.Text = "---";

            if (T3 >= 0)
                lblT3.Text = "" + T3;
            else
                lblT3.Text = "---";

            if (TN >= 0)
                lblTN.Text = "" + TN;
            else
                lblTN.Text = "---";
        }

        void UpdateStats(GSMParameters param)
        {
            BeginInvoke(new updateErrorSuccessDelegate(UpdateErrorSuccess), new object[] { param.TotalErrors, param.TotalSuccess, param.TN, param.T1, param.T2, param.T3 });
            BeginInvoke(new updateFreqOffsetDelegate(UpdateFreqOffset), new object[] { param.PhaseOffsetFrequency });
        }

        void ResetStats()
        {
            BeginInvoke(new updateErrorSuccessDelegate(UpdateErrorSuccess), new object[] { -1, -1, -1, -1, -1, -1 });
        }

        private void btnOpen_Click(object sender, EventArgs e)
        {
            if (Source != null)
            {
                ThreadActive = false;

                if (!ReadThread.Join(1000))
                    ReadThread.Abort();

                Source.Close();
                Source = null;

                btnOpen.Text = "Open";
            }
            else
            {
                ContextMenu menu = new ContextMenu();

                menu.MenuItems.Add(new MenuItem("Shared Memory", new EventHandler(btnOpen_SharedMemory)));
                menu.MenuItems.Add(new MenuItem("USRP CFile", new EventHandler(btnOpen_CFile)));
                btnOpen.ContextMenu = menu;
                btnOpen.ContextMenu.Show(btnOpen, new System.Drawing.Point(10, 10));
            }
        }

        private void btnOpen_CFile(object sender, EventArgs e)
        {
            OpenFileDialog dlg = new OpenFileDialog();
            dlg.Filter = "USRP CFiles (*.cfile)|*.cfile|All files (*.*)|*.*";

            if (dlg.ShowDialog() == DialogResult.OK)
            {
                Source = new USRPSampleSource(dlg.FileName, InternalOversampling);

                txtLog.Clear();
                ThreadActive = true;
                ReadThread = new Thread(SampleReadFunc);
                ReadThread.Start();

                btnOpen.Text = "Close";
            }
        }

        private void btnOpen_SharedMemory(object sender, EventArgs e)
        {
            Source = new ShmemSampleSource("GSM Analyzer", InternalOversampling, DefaultSamplingRate);

            txtLog.Clear();
            ThreadActive = true;
            ReadThread = new Thread(SampleReadFunc);
            ReadThread.Start();

            btnOpen.Text = "Close";
        }

        void SampleReadFunc()
        {
            FCCHFinder finder = new FCCHFinder(Oversampling);

            long frameStartPosition = 0;
            long currentPosition = 0;
            long updateLoops = 0;

            double oldSamplingRate = Source.OutputSamplingRate;

            double[] burstBuffer = new double[(int)((Handler.SpareBits + Burst.TotalBitCount) * Oversampling)];
            double[] burstStrengthBuffer = new double[(int)((Handler.SpareBits + Burst.TotalBitCount) * Oversampling)];

            long burstSamples = (long)Math.Ceiling(Burst.TotalBitCount * Oversampling);
            double burstSamplesAccurate = Burst.TotalBitCount * Oversampling;
            double deltaSamplesPerBurst = burstSamples - burstSamplesAccurate;
            double skipSampleEvery = 1 / deltaSamplesPerBurst;
            int sampleDelta = 0;


            double burstCount = 0;
            long burstBufferPos = 0;
            long lastSampleOffset = 0;



            try
            {
                while (ThreadActive)
                {
                    if (!Source.Read())
                        Thread.Sleep(100);
                    else
                    {
                        int samplesRead = Source.SamplesRead;

                        /* to allow external rate change */
                        if (Source.SamplingRateChanged)
                        {
                            Source.SamplingRateChanged = false;

                            if (Oversampling > 1)
                            {
                                AddMessage("[GSM] Sampling Rate changed from " + oldSamplingRate + " to " + Source.OutputSamplingRate + ", Oversampling factor: " + Oversampling + Environment.NewLine);
                                oldSamplingRate = Source.OutputSamplingRate;
                                finder = new FCCHFinder(Oversampling);

                                InitTimeSlotHandler();

                                burstBuffer = new double[(int)((Handler.SpareBits + Burst.TotalBitCount) * Oversampling)];
                                burstStrengthBuffer = new double[(int)((Handler.SpareBits + Burst.TotalBitCount) * Oversampling)];
                                burstSamples = (long)Math.Ceiling(Burst.TotalBitCount * Oversampling);
                                burstSamplesAccurate = Burst.TotalBitCount * Oversampling;
                                deltaSamplesPerBurst = burstSamples - burstSamplesAccurate;
                                skipSampleEvery = 1 / deltaSamplesPerBurst;
                                burstCount = 0;
                                sampleDelta = 0;

                                Parameters.Reset();
                                Parameters.Oversampling = Oversampling;
                                Parameters.BT = BT;
                                Parameters.SampleStartPosition = Oversampling * Handler.SpareBits;

                                InitTimeSlotHandler();
                                UpdateUIStatus(Parameters);
                            }
                        }


                        for (int pos = 0; pos < samplesRead; pos++)
                        {
                            double signal = Source.Signal[pos] + Parameters.PhaseOffsetValue;
                            double strength = Source.Strength[pos];

                            bool burstSampled = false;

                            /* write this sample into the burst buffer */
                            if (burstBufferPos < burstBuffer.Length && burstBufferPos > 0)
                            {
                                burstBuffer[burstBufferPos] = signal;
                                burstStrengthBuffer[burstBufferPos] = strength;
                            }
                            burstBufferPos++;

                            /* when we should skip one sample, decrease sampleDelta */
                            if (burstCount >= skipSampleEvery)
                            {
                                sampleDelta -= 1;
                                burstCount -= skipSampleEvery;
                            }

                            /* have enough samples for one burst? */
                            if (burstBufferPos >= (burstSamples + sampleDelta))
                            {
                                burstSampled = true;
                                /* reset the delta. it will get set later again */
                                sampleDelta = 0;
                            }

                            switch (Parameters.State)
                            {
                                case eGSMState.Idle:
                                    break;

                                case eGSMState.Reset:
                                    AddMessage("[GSM] Reset" + Environment.NewLine);
                                    L3Handler.ReloadFiles();
                                    currentPosition = 0;
                                    finder.Reset();
                                    Parameters.State = eGSMState.FCCHSearch;
                                    ResetStats();
                                    UpdateUIStatus(Parameters);
                                    break;

                                case eGSMState.FCCHSearch:
                                    /* let the FCCH finder detect an FCCH burst */
                                    bool fcchFound = false;
                                    try
                                    {
                                        fcchFound = finder.ProcessData(signal, strength);
                                    }
                                    catch (Exception e)
                                    {
                                        AddMessage("   [FCCH] Exception: " + e + Environment.NewLine);
                                        return;
                                    }

                                    if (fcchFound)
                                    {
                                        Parameters.State = eGSMState.SCHSearch;
                                        UpdateUIStatus(Parameters);

                                        AddMessage("   [FCCH]" + Environment.NewLine);

                                        /* save the position where the frame started */
                                        frameStartPosition = finder.BurstStartPosition;
                                        frameStartPosition -= (long)(Oversampling * Handler.SpareBits);

                                        /* update the burst buffer pointer */
                                        burstBufferPos = currentPosition - frameStartPosition;

                                        /* this is TN 0 */
                                        Parameters.FN = 0;
                                        Parameters.TN = 0;
                                    }

                                    if (burstSampled)
                                    {
                                        burstBufferPos = 0;

                                        lock (BurstWindowLock)
                                        {
                                            /* update the burst visualizer */
                                            if (BurstWindow != null)
                                            {
                                                BurstWindow.XAxisGridOffset = 0;
                                                BurstWindow.ProcessBurst(burstBuffer, burstStrengthBuffer);
                                            }
                                        }

                                        if (SingleStep)
                                            SingleStepSem.WaitOne();
                                    }
                                    break;


                                case eGSMState.SCHSearch:
                                    /* if one burst was sampled */
                                    if (burstSampled)
                                    {
                                        /* if this is the eighth burst, its the first timeslot of the next frame - SCH */
                                        if (Parameters.TN == 8)
                                        {
                                            /* set TN to 7, since handler will increase */
                                            Parameters.TN = 7;
                                            Parameters.FirstSCH = true;

                                            /* let the handler process this packet */
                                            Parameters.SampleOffset = 0;
                                            Parameters.SubSampleOffset = 0;
                                            Handler.Handle(burstBuffer);

                                            if (Parameters.ErrorLimit)
                                            {
                                                AddMessage("[GSM] SCH failed -> Reset" + Environment.NewLine);
                                                Parameters.State = eGSMState.Reset;
                                                Parameters.ResetError();
                                                UpdateUIStatus(Parameters);
                                            }
                                            else
                                            {
                                                AddMessage("[GSM] SCH found -> Lock" + Environment.NewLine);
                                                Parameters.State = eGSMState.Lock;
                                                UpdateUIStatus(Parameters);
                                            }
                                        }
                                        else
                                            Parameters.TN++;


                                        burstBufferPos = 0;

                                        lock (BurstWindowLock)
                                        {
                                            /* update the burst visualizer */
                                            if (BurstWindow != null)
                                            {
                                                BurstWindow.XAxisGridOffset = Parameters.SampleOffset;
                                                BurstWindow.ProcessBurst(burstBuffer, burstStrengthBuffer);
                                            }
                                        }

                                        if (SingleStep)
                                            SingleStepSem.WaitOne();
                                    }
                                    break;

                                case eGSMState.Lock:

                                    /* when we are already in frame sync and one burst was sampled */
                                    if (burstSampled)
                                    {
                                        //Parameters.SampleOffset = (int)(Oversampling * Handler.SpareBits);
                                        if (Subsampling)
                                            Parameters.SubSampleOffset = OffsetEstimator.EstimateOffset(burstBuffer,
                                                                                                             (int)
                                                                                                             (Parameters.
                                                                                                                  SampleOffset +
                                                                                                              Oversampling / 2 -
                                                                                                              5 * Oversampling),
                                                                                                             (int)
                                                                                                             ((Burst.
                                                                                                                   NetBitCount -
                                                                                                               5) *
                                                                                                              Oversampling),
                                                                                                             Oversampling);
                                        else
                                            Parameters.SubSampleOffset = 0;

                                        Parameters.SubSampleOffset += SubSampleOffset;

                                        lock (BurstWindowLock)
                                        {
                                            if (BurstWindow != null)
                                            {
                                                BurstWindow.SampleDisplay.DirectXLock.WaitOne();
                                                BurstWindow.SampleDisplay.YAxisLines.Clear();
                                                BurstWindow.XAxisGridOffset = ((int)(Oversampling * Handler.SpareBits) + Oversampling / 2);
                                                BurstWindow.XAxisSampleOffset = -Parameters.SubSampleOffset;
                                                BurstWindow.Oversampling = Oversampling;
                                                BurstWindow.SampleDisplay.AxisUpdated = true;
                                                BurstWindow.SampleDisplay.DirectXLock.ReleaseMutex();

                                                BurstWindow.ProcessBurst(burstBuffer, burstStrengthBuffer);
                                            }
                                        }

                                        Handler.Handle(burstBuffer);

                                        lock (BurstWindowLock)
                                        {
                                            if (BurstWindow != null)
                                            {
                                                BurstWindow.SampleDisplay.DirectXLock.WaitOne();
                                                BurstWindow.SampleDisplay.YAxisLines.Add(Handler.Decoder.MaxPower);
                                                BurstWindow.SampleDisplay.YAxisLines.Add(Handler.Decoder.DecisionPower);
                                                BurstWindow.BurstBits = Handler.BurstBits;
                                                BurstWindow.SampleDisplay.AxisUpdated = true;
                                                BurstWindow.SampleDisplay.DirectXLock.ReleaseMutex();
                                            }
                                        }

                                        if (Parameters.ErrorLimit)
                                        {
                                            AddMessage("[GSM] Packet handling failed -> Reset" + Environment.NewLine);
                                            Parameters.State = eGSMState.Reset;
                                            Parameters.ResetError();
                                            UpdateUIStatus(Parameters);
                                        }

                                        /* 
                                         * tricky! the BTS sends the bursts with 156 bits instead of 156.25
                                         * but it delays one bit after 4 bursts. compensate this here.
                                         * we do that for the next timeslot
                                         */
                                        long burstNumber = ((Parameters.TN + 1) % 4);
                                        sampleDelta += (int)(BurstLengthJitter[burstNumber] * Oversampling);

                                        /* update counters and apply offset correction */
                                        burstCount++;

                                        burstBufferPos = (long)-(Parameters.SampleOffset + Parameters.SubSampleOffset);
                                        Parameters.SampleOffset = 0;
                                        Parameters.SubSampleOffset = 0;

                                        /* update UI if necessary */
                                        if (SingleStep || updateLoops++ >= 50 * 9)
                                        {
                                            UpdateUIStatus(Parameters);
                                            UpdateStats(Parameters);
                                            updateLoops = 0;
                                        }

                                        if (SingleStep)
                                            SingleStepSem.WaitOne();
                                    }
                                    break;
                            }
                            currentPosition++;
                        }
                    }
                }
            }
            catch (ThreadAbortException e)
            {

            }
            catch (SystemException e)
            {
                MessageBox.Show("There was an unhandled SystemException." + Environment.NewLine + Environment.NewLine + "Exception:" + Environment.NewLine + e);
                AddMessage("   [GSM] SystemException: " + e + Environment.NewLine);
                return;
            }
            catch (Exception e)
            {
                MessageBox.Show("There was an unhandled Exception." + Environment.NewLine + Environment.NewLine + "Exception:" + Environment.NewLine + e);
                AddMessage("   [GSM] Exception: " + e + Environment.NewLine);
                return;
            }


            /* show statistics/information */
            AddMessage(Environment.NewLine);
            AddMessage(Environment.NewLine);
            AddMessage(Environment.NewLine);

            AddMessage(Parameters.GetSlotUsage());
            AddMessage(Environment.NewLine);
            AddMessage(Parameters.GetTimeslotDetails());
            AddMessage(Environment.NewLine);
        }

        private void InitTimeSlotHandler()
        {
            Handler = new TimeSlotHandler(Parameters, AddMessage);
            RegisterTriggers(Handler.L3);
            if (Handler.L3.StatusMessage != null)
                MessageBox.Show("   [L3] " + Handler.L3.StatusMessage);
        }

        private void btnOptions_Click(object sender, EventArgs e)
        {
            if (OptionsWindow == null || !OptionsWindow.Visible)
            {
                OptionsWindow = new OptionsDialog(this);
                OptionsWindow.Show();
            }
            else
            {
                OptionsWindow.Close();
                OptionsWindow = null;
            }
        }

        private void btnStats_Click(object sender, EventArgs e)
        {
            AddMessage(Environment.NewLine);
            AddMessage(Parameters.GetSlotUsage());

            AddMessage(Environment.NewLine);
            AddMessage(Parameters.GetTimeslotDetails());

            AddMessage(Environment.NewLine);

            /*
            if (FilterWindow == null || !FilterWindow.Visible)
            {
                FilterWindow = new FilterDialog(Handler);
                FilterWindow.Show();
            }
            else
            {
                FilterWindow.Close();
                FilterWindow = null;
            }
             * */
        }

        private void btnL3Filter_Click(object sender, EventArgs e)
        {
            if (FilterWindow == null || !FilterWindow.Visible)
            {
                FilterWindow = new FilterDialog();
                FilterWindow.Show();
            }
            else
            {
                FilterWindow.Close();
                FilterWindow = null;
            }
        }

        private void btnBurst_Click(object sender, EventArgs e)
        {
            lock (BurstWindowLock)
            {
                if (BurstWindow == null || !BurstWindow.Visible)
                {
                    try
                    {
                        BurstWindow = new BurstVisualizer(Oversampling);
                        BurstWindow.Show();
                    }
                    catch (Exception ex)
                    {
                        AddMessage("Exception while initializing Burst Window:" + Environment.NewLine + ex.ToString());
                    }
                }
                else
                {
                    BurstWindow.Close();
                    BurstWindow = null;
                }
            }
        }


        private void chkSingleStep_CheckedChanged(object sender, EventArgs e)
        {
            SingleStep = chkSingleStep.Checked;
        }

        private void btnStep_Click(object sender, EventArgs e)
        {
            try
            {
                SingleStepSem.Release(1);
            }
            catch (SemaphoreFullException ex)
            {
                /* someone clicked too fast? :) */
            }
        }

        private void btnClear_Click(object sender, EventArgs e)
        {
            txtLog.Clear();
        }
    }

}
