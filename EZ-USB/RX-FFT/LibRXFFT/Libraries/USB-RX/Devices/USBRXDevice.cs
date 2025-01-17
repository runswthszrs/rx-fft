﻿using System;
using System.Threading;
using System.Windows.Forms;
using LibRXFFT.Libraries.Misc;
using LibRXFFT.Libraries.Timers;
using LibRXFFT.Libraries.USB_RX.Interfaces;
using LibRXFFT.Libraries.USB_RX.Misc;
using LibRXFFT.Libraries.USB_RX.Tuners;
using RX_FFT.Components.GDI;
using System.Collections;
using System.Collections.Generic;

namespace LibRXFFT.Libraries.USB_RX.Devices
{
    public class USBRXDevice : I2CInterface, SPIInterface
    {
        public enum eCombinationType
        {
            Automatic,
            BO35,
            AR5000,
            VUHF_RX,
            MT2131,
            R820,
            None
        }

        private static ArrayList UsedDevNums = new ArrayList();

        private int MaxDevices = 16;
        private int DevNum = 0;
        public AD6636 AD6636;
        private eTransferMode _CurrentMode = eTransferMode.Stopped;
        public DigitalTuner Tuner;
        public Atmel Atmel;
        public AtmelProgrammer AtmelProgrammer;


        public bool UseAtmelFIFO = true;
        public eCombinationType TunerCombination = eCombinationType.None;
        private int FIFOResetPortPin = 3;

        private object Lock = new object();

        private bool PreQueueTransfer = true;

        public uint SamplesPerBlock { get; set; }

        public uint ReadFragmentSize = 0;
        private uint _ReadBlockSize = 4096;
        public uint ReadBlockSize
        {
            get { return _ReadBlockSize; }
            set
            {
                _ReadBlockSize = value;
                if (CurrentMode == eTransferMode.Block)
                {
                    lock (Lock)
                    {
                        USBRXDeviceNative.UsbSetControlledTransfer(DevNum, 0, ReadBlockSize);
                    }
                }
            }
        }

        public double BlocksPerSecond
        {
            get
            {
                return 1000 / (double)ReadTimer.Interval;
            }

            set
            {
                ReadTimer.Interval = (uint)(1000 / value);
            }
        }

        public int ShmemChannel
        {
            get
            {
                return USBRXDeviceNative.UsbGetShmemChannel(DevNum);
            }
        }

        public int ShmemNode
        {
            get
            {
                return USBRXDeviceNative.UsbGetShmemID(DevNum);
            }
        }

        public USBRXDevice()
        {
            ReadTimer = new AccurateTimer();
            ReadTimer.Interval = 50;
//            ReadTimer.Type = AccurateTimer.eEventType.Wait;
            ReadTimer.Timer += new EventHandler(ReadTimer_Timer);
        }

        public bool Init()
        {
            try
            {
                //ShowConsole(true);

                Tuner mainTuner = null;
                long stepSize = 0;

                bool success = false;
                BO35 bo35 = null;
                AR5000N ar5000 = null;
                MT2131 mt2131 = null;
                USBRX_R820 r820 = null;
                VUHF_RX vuhfrx = null;

                if (mainTuner == null && (TunerCombination == eCombinationType.BO35 || TunerCombination == eCombinationType.Automatic))
                {
                    /* try to open BO-35 */
                    bo35 = new BO35(true);

                    try
                    {
                        success = bo35.OpenTuner();
                    }
                    catch (Exception e)
                    {
                    }

                    if (success)
                    {
                        stepSize = 0;
                        mainTuner = bo35;
                    }
                    else if (TunerCombination == eCombinationType.BO35)
                    {
                        return false;
                    }
                }
                if (mainTuner == null && (TunerCombination == eCombinationType.AR5000 || TunerCombination == eCombinationType.Automatic))
                {
                    /* try to open BO-35 */
                    ar5000 = new AR5000N("AR5000");

                    try
                    {
                        success = ar5000.OpenTuner();
                    }
                    catch (Exception e)
                    {
                    }

                    if (success)
                    {
                        stepSize = 0;
                        mainTuner = ar5000;
                    }
                    else if (TunerCombination == eCombinationType.AR5000)
                    {
                        return false;
                    }
                }


                lock (Lock)
                {
                    DevNum = GetFreeDeviceNum();

                    if (USBRXDeviceNative.UsbInit(DevNum))
                    {
                        if (!UseAtmelFIFO)
                        {
                            USBRXDeviceNative.UsbSetIODir(DevNum, FIFOResetPortPin, USBRXDeviceNative.PIN_DIR_OUT);
                        }

                        /* we will handle ext fifo flushing ourselves */
                        USBRXDeviceNative.SetFifoFlushing(false);

                        /* set maximum I2C speed */
                        USBRXDeviceNative.UsbI2CSetSpeed(DevNum, 1);

                        /* init low level interface to atmel */
                        AtmelProgrammer = new AtmelProgrammer(this);
                        AtmelProgrammer.ResetAtmel();

                        Atmel = new Atmel(this);
                        if (Atmel.Exists)
                        {
                            AD6636 = new AD6636(Atmel, Atmel.TCXOFreq);
                            Tuner = AD6636;

                            /* detect I2C tuners */
                            if (mainTuner == null && (TunerCombination == eCombinationType.MT2131 || TunerCombination == eCombinationType.Automatic))
                            {
                                mt2131 = new MT2131(this);

                                try
                                {
                                    success = mt2131.OpenTuner();
                                }
                                catch (Exception e)
                                {
                                }

                                if (success)
                                {
                                    stepSize = mt2131.IFStepSize;
                                    mainTuner = mt2131;
                                }
                                else if (TunerCombination == eCombinationType.MT2131)
                                {
                                    ReleaseDeviceNum(DevNum);
                                    return false;
                                }
                            }

                            if (mainTuner == null && (TunerCombination == eCombinationType.R820 || TunerCombination == eCombinationType.Automatic))
                            {
                                r820 = new USBRX_R820(this);
                                try
                                {
                                    success = r820.OpenTuner();
                                }
                                catch (Exception e)
                                {
                                }

                                if (success)
                                {
                                    stepSize = r820.IFStepSize;
                                    mainTuner = r820;
                                }
                                else if (TunerCombination == eCombinationType.MT2131)
                                {
                                    ReleaseDeviceNum(DevNum);
                                    return false;
                                }
                            }

                            

                            if (mainTuner == null && (TunerCombination == eCombinationType.VUHF_RX || TunerCombination == eCombinationType.Automatic))
                            {
                                vuhfrx = new VUHF_RX(this);

                                try
                                {
                                    success = vuhfrx.OpenTuner();
                                }
                                catch (Exception e)
                                {
                                }

                                if (success)
                                {
                                    stepSize = vuhfrx.IFStepSize;
                                    mainTuner = vuhfrx;
                                }
                                else if (TunerCombination == eCombinationType.VUHF_RX)
                                {
                                    ReleaseDeviceNum(DevNum);
                                    return false;
                                }
                            }

                            if (mainTuner != null)
                            {
                                Tuner = new TunerStack(mainTuner, Tuner, stepSize);
                            }

                            Tuner.OpenTuner();
                        }

                        //SetAgc(eAgcType.Slow);

                        CurrentMode = eTransferMode.Stopped;

                        StartThreads();

                        SetAtt(0);

                        return true;
                    }
                    else
                    {
                        ReleaseDeviceNum(DevNum);
                    }
                }
            }
            catch (DllNotFoundException e)
            {
                MessageBox.Show("Was not able to load the driver. The driver DLL was not found." + Environment.NewLine + Environment.NewLine + e.Message);
            }
            catch (Exception e)
            {
                MessageBox.Show("Was not able to load the driver:" + Environment.NewLine + Environment.NewLine + e.Message);
            }

            return false;
        }

        private int GetFreeDeviceNum()
        {
            int dev = 0;

            /* allocate a free device id */
            lock (UsedDevNums)
            {
                while (UsedDevNums.Contains(dev) || !USBRXDeviceNative.UsbDevicePresent(dev) )
                {
                    dev++;
                    if (dev > MaxDevices)
                    {
                        return -1;
                    }
                }

                UsedDevNums.Add(dev);
            }

            return dev;
        }

        private static void ReleaseDeviceNum(int dev)
        {
            lock (UsedDevNums)
            {
                if (UsedDevNums.Contains(dev))
                {
                    UsedDevNums.Remove(dev);
                }
            }
        }

        private void StartThreads()
        {
            lock (this)
            {
                StopThreads();

                ReadThread = new Thread(ReadThreadMain);
                ReadThread.Name = "USB-RX Block read thread";
                ReadThread.Start();

                ReadTriggerThread = new Thread(ReadTriggerThreadMain);
                ReadTriggerThread.Name = "USB-RX Block read trigger thread";
                ReadTriggerThread.Start();
            }
        }

        private void StopThreads()
        {
            lock (this)
            {
                if (ReadThread != null)
                {
                    ReadThread.Abort();
                    ReadThread = null;
                }
                if (ReadTriggerThread != null)
                {
                    ReadTriggerThread.Abort();
                    ReadTriggerThread = null;
                }
            }
        }

        public void ShowConsole(bool show)
        {
            ushort mode = (ushort)(USBRXDeviceNative.MODE_NORMAL | USBRXDeviceNative.MODE_FASTI2C);

            if (show)
            {
                mode |= USBRXDeviceNative.MODE_CONSOLE;
            }

            lock (Lock)
            {
                USBRXDeviceNative.UsbSetTimeout(0x80 | DevNum, mode);
            }
        }

        public eTransferMode CurrentMode
        {
            get { return _CurrentMode; }
            set
            {
                eTransferMode lastMode = _CurrentMode;
                _CurrentMode = value;

                PreQueueTransfer = false;

                switch (value)
                {
                    case eTransferMode.Block:
                        if (lastMode == eTransferMode.Stream)
                        {
                            StopStreamRead();
                        }
                        else
                        {
                            StopRead();
                        }
                        StartRead();
                        break;

                    case eTransferMode.Stream:
                        if (lastMode == eTransferMode.Stream)
                        {
                            StopStreamRead();
                        }
                        else
                        {
                            StopRead();
                        }
                        StartStreamRead();
                        break;

                    case eTransferMode.Stopped:
                        if (lastMode == eTransferMode.Stream)
                        {
                            StopStreamRead();
                        }
                        else
                        {
                            StopRead();
                        }
                        break;
                }
            }
        }


        #region Abstraction

        public enum eAgcType
        {
            Off,
            Slow,
            Medium,
            Fast,
            Manual
        }

        public enum eRfSource
        {
            Tuner,
            InternalTuner,
            RF1,
            RF2,
            RF3,
            RF4
        }

        public bool SetFilter(AtmelFilter filter)
        {
            if (AD6636 == null)
                return false;

            if (!Atmel.SetFilter(filter.Id))
                return false;

            FIFOReset(true);
            AD6636.SetFilter(filter);
            AD6636.SoftSync();
            FIFOReset(false);


            return true;
        }

        public bool SetFilter(AD6636FilterFile filter)
        {
            if (AD6636 == null)
                return false;

            return AD6636.SetFilter(filter);
        }

        public bool SetAgc(eAgcType type)
        {
            SetMgc(0);
            //AD6636.SetAgc();
            return Atmel.SetAgc(type);
        }

        public bool SetMgc(int dB)
        {
            return AD6636.SetMgcValue(dB);
        }

        public bool SetRfSource(eRfSource source)
        {
            return Atmel.SetRfSource(source);
        }

        public bool SetAtt(bool state)
        {
            return Atmel.SetAtt(state);
        }

        public bool SetAtt(int value)
        {
            return Atmel.SetAtt(value);
        }

        public bool SetPreAmp(bool state)
        {
            return Atmel.SetPreAmp(state);
        }

        #endregion

        #region Transfer

        private AccurateTimer ReadTimer;
        private Thread ReadThread;
        private Thread ReadTriggerThread;
        private object ReadTriggerTrigger = new object();
        private object ReadTrigger = new object();
        private object ReadTimerLock = new object();

        private bool ReadTriggered = false;
        private bool ReadTimerLocked = false;

        private double ExpectedReadDuration = 0;
        private int SleepDuration = 10;
        private int MaxOvertimeFactor = 20;
        private int TimeoutsHappened = 0;
        public bool DeviceLost = false;

        private void ReadTriggerThreadMain()
        {
            try
            {
                while (true)
                {
                    lock (ReadTriggerTrigger)
                    {
                        Monitor.Wait(ReadTriggerTrigger);
                    }
                    //Log.AddMessage("ReadTriggerTrigger [was fired]");

                    lock (ReadTimerLock)
                    {
                        lock (ReadTrigger)
                        {
                            int loops = 0;
                            int maxLoops = (int)(MaxOvertimeFactor * 1000 * ExpectedReadDuration / SleepDuration);
                            bool timeout = false;

                            /* dont fire next read until last data was processed */
                            while (ReadTimerLocked && !timeout)
                            {
                                /* minimum once again */
                                timeout = (loops++ > maxLoops);
                                Monitor.Wait(ReadTimerLock, SleepDuration);
                            }

                            if (timeout)
                            {
                                TimeoutsHappened++;
                                Log.AddMessage("TIMEOUT! Expected " + FrequencyFormatter.TimeToString(ExpectedReadDuration) + ", stopped after " + FrequencyFormatter.TimeToString(((double)(loops * SleepDuration) / 1000)));
                                HandleTimeout();
                            }
                            //Log.AddMessage("ReadTimerLocked [false]");

                            ReadTriggered = true;
                            //Log.AddMessage("ReadTrigger [fire]");
                            Monitor.Pulse(ReadTrigger);
                        }
                    }
                }
            }
            catch (ThreadAbortException ex)
            {
                return;
            }
        }

        private void HandleTimeout()
        {
            return;
            new Thread(() =>
                {
                    switch (TimeoutsHappened - 4)
                    {
                        case 1:
                        case 2:
                            Log.AddMessage("USBRXDevice: Timeout " + TimeoutsHappened + ". Retrigger Transfer");
                            lock (Lock)
                            {
                                USBRXDeviceNative.UsbSetGPIFMode(DevNum);
                            }
                            lock (ReadTimerLock)
                            {
                                Monitor.Pulse(ReadTimerLock);
                            }

                            break;

                        case 6:
                            Log.AddMessage("USBRXDevice: Timeout " + TimeoutsHappened + ". Reinit AD6636");

                            AD6636.ReInit();
                            break;

                        case 3:
                        case 7:
                            Log.AddMessage("USBRXDevice: Timeout " + TimeoutsHappened + ". Reset AD6636");

                            Atmel.AD6636Reset();
                            AD6636.ReInit();
                            AD6636.SoftSync();
                            FIFOReset(false);
                            break;

                        case 4:
                        case 8:
                            Log.AddMessage("USBRXDevice: Timeout " + TimeoutsHappened + ". Resync AD6636, reset Atmel");

                            AD6636.SoftSync();
                            SPIReset(true);
                            SPIReset(false);
                            Thread.Sleep(50);
                            FIFOReset(false);
                            break;

                        case 9:
                            Log.AddMessage("USBRXDevice: Timeout " + TimeoutsHappened + ". Reset Threads");

                            new Thread(() =>
                            {
                                CurrentMode = CurrentMode;
                                FIFOReset(false);
                            }).Start();
                            /*
                            FIFOReset(true);
                            USBRXDeviceNative.UsbSetIdleMode(DevNum);
                            USBRXDeviceNative.UsbSetGPIFMode(DevNum);
                            USBRXDeviceNative.SetSlaveFifoParams(true, DevNum, 0);
                            FIFOReset(false);
                            */
                            break;


                        case 12:
                            DeviceLost = true;
                            break;
                    }
                }).Start();
        }

        private void ReadThreadMain()
        {
            try
            {
                lock (ReadTrigger)
                {
                    while (true)
                    {
                        /* when read timer fires */
                        while (!ReadTriggered)
                        {
                            Monitor.Wait(ReadTrigger, 50);
                        }
                        //Log.AddMessage("ReadTrigger [was fired]");

                        ReadTriggered = false;

                        /* start a new transfer */

                        if (!PreQueueTransfer)
                        {
                            //PreQueueTransfer = true;
                            //Log.AddMessage("ReadTrigger [new transfer]");
                            lock (Lock)
                            {
                                USBRXDeviceNative.ResetEpFifo(DevNum);
                                USBRXDeviceNative.UsbSetControlledTransfer(DevNum, ReadBlockSize, ReadFragmentSize);
                            }
                            FIFOReset(false);
                        }

                        ExpectedReadDuration = SamplesPerBlock / (double)Tuner.SamplingRate;

                        /* dont fire next read until data was processed */
                        ReadTimerLocked = true;
                    }
                }
            }
            catch (ThreadAbortException ex)
            {
                return;
            }
        }

        private void ReadTimer_Timer(object sender, EventArgs e)
        {
            //Log.AddMessage("ReadTimer_Timer [fired]");
            lock (ReadTriggerTrigger)
            {
                Monitor.Pulse(ReadTriggerTrigger);
            }
        }

        public void ReadBlockReceived()
        {
            lock (ReadTimerLock)
            {
                TimeoutsHappened = 0;
                DeviceLost = false;

                FIFOReset(true);
                USBRXDeviceNative.ResetEpFifo(DevNum);

                if (PreQueueTransfer)
                {
                    //PreQueueTransfer = true;
                    //Log.AddMessage("ReadTrigger [new transfer]");
                    lock (Lock)
                    {
                        USBRXDeviceNative.UsbSetControlledTransfer(DevNum, ReadBlockSize, ReadFragmentSize);
                    }
                    FIFOReset(false);
                }


                ReadTimerLocked = false;
                Monitor.Pulse(ReadTimerLock);
            }
        }

        private bool FIFOReset(bool state)
        {
            if (UseAtmelFIFO)
            {
                return Atmel.FIFOReset(state);
            }

            lock (Lock)
            {
                return USBRXDeviceNative.UsbSetIOState(DevNum, FIFOResetPortPin, state ? USBRXDeviceNative.PIN_STATE_HIGH : USBRXDeviceNative.PIN_STATE_LOW);
            }
        }

        private void StartStreamRead()
        {
            StartThreads();

            lock (Lock)
            {
                USBRXDeviceNative.ResetEpFifo(DevNum);
                USBRXDeviceNative.UsbSetGPIFMode(DevNum);
                USBRXDeviceNative.UsbSetControlledTransfer(DevNum, 0, ReadBlockSize);
            }

            FIFOReset(false);
        }

        private void StopStreamRead()
        {
            FIFOReset(true);

            lock (Lock)
            {
                USBRXDeviceNative.UsbSetControlledTransfer(DevNum, ReadBlockSize, ReadFragmentSize);
                USBRXDeviceNative.UsbSetGPIFMode(DevNum);
            }

            StopThreads();
        }

        private void StartRead()
        {
            StartThreads();

            lock (Lock)
            {
                USBRXDeviceNative.ResetEpFifo(DevNum);
                USBRXDeviceNative.UsbSetGPIFMode(DevNum);
            }
            ReadTimerLocked = false;
            ReadTimer.Start();

            lock (ReadTimerLock)
            {
                Monitor.Pulse(ReadTimerLock);
            }
        }

        private void StopRead()
        {
            FIFOReset(true);

            ReadTimer.Stop();
            ReadTimerLocked = false;

            StopThreads();
        }

        public void Close()
        {
            if (Tuner != null)
            {
                Tuner.CloseTuner();
            }

            /* stop read timer */
            ReadTimer.Stop();

            /* stop read trigger thread */
            StopThreads();

            /* close driver handle */
            lock (Lock)
            {
                USBRXDeviceNative.UsbClose(DevNum);
                ReleaseDeviceNum(DevNum);
            }
        }

        public bool Read(byte[] data)
        {
            lock (Lock)
            {
                return USBRXDeviceNative.UsbParIn(DevNum, data, (uint)data.Length);
            }
        }

        #endregion

        #region I2CInterface Member

        private object I2CAccessLock = new object();
        private HighPerformanceCounter Counter = null;
        public int I2CRetries = 5;
        public int I2CSleep = 50;

        public enum eDirection
        {
            Read,
            Write
        }

        public class I2CDataEntry
        {
            public eDirection Direction;
            public int Device;
            public int Length;
            public byte[] Data;
            public double Timestamp;
        }

        public LinkedList<I2CDataEntry> I2CAccesses;

        private void AddAccess(eDirection dir, int device, int length, byte[] data)
        {
            if (Counter == null)
            {
                Counter = new HighPerformanceCounter("I²C Debug");
                Counter.Start();
            }
            if (I2CAccesses == null)
            {
                I2CAccesses = new LinkedList<I2CDataEntry>();
            }

            lock (I2CAccesses)
            {
                I2CDataEntry entry = new I2CDataEntry();
                entry.Direction = dir;
                entry.Device = device;
                entry.Length = length;
                entry.Data = data;

                Counter.Stop();
                entry.Timestamp = Counter.Duration;
                Counter.Start();

                I2CAccesses.AddLast(entry);
            }
        }

        public bool I2CWriteByte(int busID, byte data)
        {
            //AddAccess(eDirection.Write, busID, 1, new[] { data });

            int tries = I2CRetries;
            lock (Lock)
            {
                do
                {
                    Thread.Sleep(5);
                    if (USBRXDeviceNative.UsbI2CWriteByte(DevNum, busID, data))
                    {
                        return true;
                    }

                    if (tries == 0)
                    {
                        DeviceLost = true;
                        return false;
                    }
                    Thread.Sleep(I2CSleep);
                } while (tries-- > 0);
            }
            return false;
        }

        public bool I2CWriteBytes(int busID, byte[] data)
        {
            //DiscardNextBlock = true;
            //AddAccess(eDirection.Write, busID, data.Length, data);

            int retries = I2CRetries;
            lock (Lock)
            {
                do
                {
                    //Thread.Sleep(5);
                    if (USBRXDeviceNative.UsbI2CWriteBytes(DevNum, busID, (ushort)data.Length, data))
                    {
                        return true;
                    }

                    if (retries == 0)
                    {
                        DeviceLost = true;
                        return false;
                    }
                    Thread.Sleep(I2CSleep);
                } while (retries-- > 0);
            }
            return false;
        }

        public bool I2CReadByte(int busID, byte[] data)
        {
            //AddAccess(eDirection.Write, busID, 1, null);

            int retries = I2CRetries;
            lock (Lock)
            {
                do
                {
                    //Thread.Sleep(5);
                    if (USBRXDeviceNative.UsbI2CReadByte(DevNum, busID, data))
                    {
                        return true;
                    }

                    if (retries == 0)
                    {
                        DeviceLost = true;
                        return false;
                    }
                    Thread.Sleep(I2CSleep);
                } while (retries-- > 0);
            }
            return false;
        }

        public bool I2CReadBytes(int busID, byte[] data)
        {
            //DiscardNextBlock = true;
            //AddAccess(eDirection.Read, busID, data.Length, null);

            int retries = I2CRetries;
            lock (Lock)
            {
                do
                {
                    //Thread.Sleep(5);
                    if (USBRXDeviceNative.UsbI2CReadBytes(DevNum, busID, (ushort)data.Length, data))
                    {
                        return true;
                    }

                    if (retries == 0)
                    {
                        DeviceLost = true;
                        return false;
                    }
                    Thread.Sleep(I2CSleep);
                } while (retries-- > 0);
            }
            return false;
        }

        public bool I2CDeviceAck(int busID)
        {
            lock (Lock)
            {
                return USBRXDeviceNative.UsbI2CWriteBytes(DevNum, busID, 0, null);
            }
        }

        public void I2CSetTimeout(ushort timeout, ushort retries)
        {
            lock (Lock)
            {
                USBRXDeviceNative.UsbI2CSetTimeout(DevNum, (ushort)(((retries & 0xFF) << 8) | (timeout & 0xFF)));
            }
        }
        
        public void I2CSetSpeed(bool fast)
        {
            USBRXDeviceNative.UsbI2CSetSpeed(DevNum, fast ? 1 : 0);
        }

        #endregion

        #region SPIInterface Member

        public void SPIInit()
        {
            lock (Lock)
            {
                USBRXDeviceNative.UsbSetIODir(DevNum, USBRXDeviceNative.PIN_SPI_RESET, USBRXDeviceNative.PIN_DIR_OUT); // RESET
                USBRXDeviceNative.UsbSetIODir(DevNum, USBRXDeviceNative.PIN_SPI_SDO, USBRXDeviceNative.PIN_DIR_OUT); // SDO
                USBRXDeviceNative.UsbSetIODir(DevNum, USBRXDeviceNative.PIN_SPI_CLK, USBRXDeviceNative.PIN_DIR_OUT); // CLK
                USBRXDeviceNative.UsbSetIODir(DevNum, USBRXDeviceNative.PIN_SPI_SDI, USBRXDeviceNative.PIN_DIR_IN); // SDI
                USBRXDeviceNative.UsbSetIODir(DevNum, USBRXDeviceNative.PIN_SPI_LED_IN, USBRXDeviceNative.PIN_DIR_IN); // LED pin

                USBRXDeviceNative.UsbSetIOState(DevNum, USBRXDeviceNative.PIN_SPI_RESET, USBRXDeviceNative.PIN_STATE_HIGH); // RESET inactive
            }
        }

        public bool SPIResetDevice()
        {
            return true;
        }

        public bool SPITransfer(byte[] dataWrite, byte[] dataRead)
        {
            lock (Lock)
            {
                return USBRXDeviceNative.UsbSpiTransfer(DevNum, dataWrite, dataRead, (ushort)dataWrite.Length);
            }
        }

        public bool SPIReset(bool state)
        {
            lock (Lock)
            {
                return USBRXDeviceNative.UsbSetIOState(DevNum, USBRXDeviceNative.PIN_SPI_RESET, state ? USBRXDeviceNative.PIN_STATE_LOW : USBRXDeviceNative.PIN_STATE_HIGH);
            }
        }

        #endregion

    }
}
