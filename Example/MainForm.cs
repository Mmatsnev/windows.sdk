﻿using System;
using System.Windows.Forms;

using Ibox.Pro.SDK.External;
using System.Drawing;
using System.Linq;
using System.Collections.Generic;
using Ibox.Pro.SDK.External.Result;
using System.IO;
using System.Management;
using Ibox.Pro.SDK.External.Context;
using System.Threading.Tasks;
using System.Threading;
using System.IO.Ports;
using InTheHand.Net.Sockets;
using InTheHand.Net;

namespace Example
{
    public partial class MainForm : Form
    {
        private const string PRODUCT_CODE = "PRODUCT_TEST";
        private const string PRODUCT_FIELD_1_CODE = "FIELD_1";
        private const string PRODUCT_FIELD_2_CODE = "FIELD_2";

        private Font fntRegular = new Font("Courier New", 8.25F);
        private Font fntStrikeout = new Font("Courier New", 8.25F, FontStyle.Strikeout);

        private PaymentController m_PaymentController = PaymentController.Instance;
        private List<PortInfo> portInfos = new List<PortInfo>();
        private string divider = new string('=', 93);

        public MainForm()
        {
            InitializeComponent();
            initControls();

            m_PaymentController.SelectApplicationDelegate = onRequestSelectApplication;
            m_PaymentController.ConfirmScheduleDelegate = onRequestConfirmSchedule;
            m_PaymentController.ScheduleCreationFailedDelegate = onScheduleCreationFailed;

            m_PaymentController.ErrorEvent += onPaymentError;
            m_PaymentController.ReaderEvent += onReaderEvent;
            m_PaymentController.TransactionStartedEvent += onTransactionStarted;
            m_PaymentController.TransactionFinishedEvent += onPaymentFinished;
            m_PaymentController.ReverseEvent += onReverseEvent;
        }

        private void initControls()
        {
            cbl_RepeatType.Items.AddRange(Enum.GetValues(typeof(RepeatType)).Cast<object>().ToArray());
            cbl_End.Items.AddRange(Enum.GetValues(typeof(EndType)).Cast<object>().ToArray());

            dtp_StartDate.MinDate = DateTime.Today;
            dtp_EndDate.MinDate = DateTime.Today;

            dtp_Time.Format = DateTimePickerFormat.Custom;
            dtp_Time.CustomFormat = "HH:mm";
            dtp_Time.ShowUpDown = true;

            cbl_Month.Items.AddRange(System.Globalization.CultureInfo.CurrentUICulture.DateTimeFormat.MonthNames);
            cbl_DayOfWeek.Items.AddRange(System.Globalization.CultureInfo.CurrentUICulture.DateTimeFormat.DayNames);
            cbl_Month.SelectedIndex = 0;

            int countOfDays = DateTime.DaysInMonth(1980, cbl_Month.SelectedIndex + 1);
            object[] days = new object[countOfDays + 1];
            for (int i = 0; i < countOfDays; i++)
                days[i] = (i + 1).ToString();
            days[countOfDays] = "Last day of month";
            cbl_Day.Items.AddRange(days);

            cbl_RepeatType.SelectedIndex = 0;
            cbl_End.SelectedIndex = 0;
            cbl_QMonth.SelectedIndex = 0;
            cbl_DayOfWeek.SelectedIndex = 0;
            cbl_Day.SelectedIndex = countOfDays;
        }

        private void setCredentials()
        {
            m_PaymentController.SetCredentials(edt_Login.Text, edt_Password.Text);
        }

        private void log(string log, Color? color = null, bool strikethrough = false)
        {
            try
            {
                if (!this.Disposing && !this.IsDisposed)
                    this.Invoke((MethodInvoker)delegate
                        {
                            try
                            {
                                if (!edt_Log.Disposing && !edt_Log.IsDisposed)
                                {

                                    if (color == null)
                                        edt_Log.SelectionColor = Color.Black;
                                    else
                                        edt_Log.SelectionColor = (Color)color;

                                    if (strikethrough)
                                        edt_Log.SelectionFont = fntStrikeout;
                                    else
                                        edt_Log.SelectionFont = fntRegular;

                                    edt_Log.AppendText(log + Environment.NewLine);
                                }
                            }
                            catch (ObjectDisposedException e)
                            {

                            }
                        });
            }
            catch (ObjectDisposedException e)
            {

            }
        }

        private void startPayment()
        {
            setCredentials();

            PaymentController.Instance.SinglestepEMV = cb_SinglestepEMV.Checked;
            bool hasProduct = cb_Product.Checked;
            bool isRegular = cb_Regular.Checked;
            PaymentContext paymentContext = isRegular ? new RegularPaymentContext() : new PaymentContext();
            paymentContext.Amount = decimal.Parse(edt_Amount.Text);
            paymentContext.Currency = rb_RUB.Checked ? Currency.RUB : Currency.VND;
            paymentContext.Description = edt_Description.Text;
            paymentContext.Cash = cb_Cash.Checked;

            string path = edt_ImageFilePath.Text;
            if (!string.IsNullOrEmpty(path))
            {
                if (File.Exists(path))
                {
                    try
                    {
                        paymentContext.Image = File.ReadAllBytes(path);
                    }
                    catch (Exception e)
                    {
                        log("ERROR : CANT READ IMAGE");
                        return;
                    }
                }
            }

            if (hasProduct)
            {
                paymentContext.PaymentProductCode = PRODUCT_CODE;
                var paymentProductTextData = new Dictionary<string, string>(2);
                paymentProductTextData.Add(PRODUCT_FIELD_1_CODE, edt_Field1.Text);

                var paymentProductImageData = new Dictionary<string, byte[]>(1);
                path = edt_Field2.Text;
                if (!string.IsNullOrEmpty(path))
                {
                    if (File.Exists(path))
                    {
                        try
                        {
                            paymentProductImageData.Add(PRODUCT_FIELD_2_CODE, File.ReadAllBytes(path));
                        }
                        catch (Exception e)
                        {
                            log(string.Format("ERROR : CANT READ IMAGE ({0})", PRODUCT_FIELD_2_CODE));
                            return;
                        }
                    }
                }
                paymentContext.PaymentProductTextDictionary = paymentProductTextData;
                paymentContext.PaymentProductImageDictionary = paymentProductImageData;
            }

            if (isRegular)
            {
                RegularPaymentContext regPaymentContext = (paymentContext as RegularPaymentContext);
                regPaymentContext.PaymentRepeatType = (RepeatType)cbl_RepeatType.SelectedIndex;
                regPaymentContext.PaymentEndType = (EndType)cbl_End.SelectedIndex;
                regPaymentContext.StartDate = dtp_StartDate.Value;
                regPaymentContext.EndDate = dtp_EndDate.Value;
                regPaymentContext.RepeatCount = int.Parse(edt_RepeatCount.Text);
                regPaymentContext.MonthOfQuarter = cbl_QMonth.SelectedIndex + 1;
                regPaymentContext.Month = cbl_Month.SelectedIndex + 1;

                try
                {
                    regPaymentContext.Day = int.Parse(cbl_Day.Text);
                }
                catch (FormatException ex)
                {
                    regPaymentContext.Day = RegularPaymentContext.LAST_DAY_OF_MONTH;
                }

                regPaymentContext.DayOfWeek = cbl_DayOfWeek.SelectedIndex;
                regPaymentContext.Hour = dtp_Time.Value.Hour;
                regPaymentContext.Minute = dtp_Time.Value.Minute;
                if (string.IsNullOrEmpty(edt_Email.Text) && string.IsNullOrEmpty(edt_Phone.Text))
                {
                    log("ERROR : email or phone required");
                    return;
                }
                regPaymentContext.ReceiptEmail = edt_Email.Text;
                regPaymentContext.ReceiptPhone = edt_Phone.Text;

                if (!string.IsNullOrEmpty(edt_ArbitraryDays.Text))
                {
                    string[] days = edt_ArbitraryDays.Text.Split(new char[] { ';' });
                    foreach (string day in days)
                        regPaymentContext.ArbitraryDays.Add(DateTime.ParseExact(day.Trim(), "dd.MM.yyyy", System.Globalization.CultureInfo.InvariantCulture));
                }
            }

            try
            {
                m_PaymentController.StartPayment(paymentContext);
                log(divider);
                log(string.Format("STARTING NEW PAYMENT : {0}{1}", Environment.NewLine, paymentContext.ToString()));
            }
            catch (InvalidOperationException e)
            {
                log(string.Format("ERROR : {0}", e.Message));
                return;
            }

        }

        private void reversePayment()
        {
            setCredentials();

            try
            {
                String trID = edt_ReverseID.Text;
                ReverseMode mode = rb_Cancel.Checked ? ReverseMode.Cancel : ReverseMode.Return;
                decimal? amountToReverse = string.IsNullOrEmpty(edt_ReverseAmount.Text) ? null : (decimal?)decimal.Parse(edt_ReverseAmount.Text);
                m_PaymentController.StartReverse(trID, mode, amountToReverse);
                log(divider);
                log(string.Format("{0} PAYMENT : {1}{2}", mode == ReverseMode.Cancel ? "CANCEL" : "RETURN", Environment.NewLine, trID));
            }
            catch (InvalidOperationException e)
            {
                log(string.Format("ERROR : {0}", e.Message));
                return;
            }
        }

        private void adjustPayment()
        {
            log(divider);
            log("STARTING ADJUST");

            setCredentials();
            
            Task adjustTask = Task.Factory.StartNew(() =>
            {
                APIResult result = null;
                if (rb_AdjustSimple.Checked)
                {
                    result = PaymentController.Instance.Adjust(edt_AdjustTrId.Text, edt_AdjustEmail.Text, edt_AdjustPhone.Text);
                }
                else if (rb_AdjustRegular.Checked)
                {
                    result = PaymentController.Instance.AdjustRegular(edt_AdjustTrId.Text, edt_AdjustEmail.Text, edt_AdjustPhone.Text);
                }
                else if (rb_AdjustReverse.Checked)
                {
                    result = PaymentController.Instance.AdjustReverse(edt_AdjustTrId.Text, edt_AdjustEmail.Text, edt_AdjustPhone.Text);
                }
                if (result != null && result.ErrorCode == 0)
                    log("ADJUST FINISHED OK");
                else
                    log(string.Format("ADJUST ERROR : {0}({1})", (result == null ? "null" : result.ErrorMessage), (result == null ? "null" : result.ErrorCode.ToString())));
                log(divider);
            });            
        }

        private void getHistory()
        {
            setCredentials();
            int page = 0;
            try
            {
                page = int.Parse(edt_HistoryPage.Text);
            }
            catch (FormatException ex)
            {
            }

            log(divider);
            log(string.Format("GET HISTORY PAGE #{0} :", page));

            Task getHistoryTask = Task.Factory.StartNew(() =>
            {
                APIGetHistoryResult result = PaymentController.Instance.GetHistory(page);
                if (result != null && result.ErrorCode == 0)
                {
                    log(string.Format("{0,-18}  {1,-25} {2,-10} {3}", "DateTime", "Description", "Balance", "ID"));
                    if (result.Transactions != null)
                        foreach (Ibox.Pro.SDK.External.Entry.Transaction transaction in result.Transactions)
                        {
                            Color color = Color.Black;
                            switch (transaction.DisplayMode)
                            {
                                case Ibox.Pro.SDK.External.Entry.DisplayMode.Success:
                                    color = Color.Green;
                                    break;
                                case Ibox.Pro.SDK.External.Entry.DisplayMode.Reverse:
                                case Ibox.Pro.SDK.External.Entry.DisplayMode.Reversed:
                                    color = Color.SlateGray;
                                    break;
                                case Ibox.Pro.SDK.External.Entry.DisplayMode.Declined:
                                    color = Color.OrangeRed;
                                    break;
                            }
                            log(string.Format("{0,-17:dd.MM.yyyy hh:mm}   {1,-25} {2,-10} {3}", 
                                transaction.Date, transaction.Description, string.Format(transaction.AmountFormat, transaction.Balance), transaction.ID),
                                color, !transaction.Canceled);
                        }
                }
                else
                {
                    log(string.Format("GET HISTORY ERROR : {0}({1})", (result == null ? "null" : result.ErrorMessage), (result == null ? "null" : result.ErrorCode.ToString())));
                }

                log(divider);
            });                   
        }

        private int onRequestSelectApplication(List<string> apps)
        {
            log("REQUEST SELECT APP");
            log(string.Join(Environment.NewLine, apps));
            return 1;
        }

        private bool onRequestConfirmSchedule(List<KeyValuePair<DateTime, decimal>> steps, decimal totalAmount)
        {
            log("REQUEST CONFIRM SCHEDULE :");
            log(string.Join(Environment.NewLine, steps));
            log(string.Format("Total : {0}", totalAmount));
            return MessageBox.Show("Confirm schedule", "Schedule confirmation", MessageBoxButtons.YesNo) == DialogResult.Yes;
        }

        private bool onScheduleCreationFailed(PaymentError error, string description = null)
        {
            log(String.Format("PAYMENT CREATION FAILED : {0}({1})", error, description ?? ""));
            return MessageBox.Show("Payment creation failed. Retry?", "Payment creation failed", MessageBoxButtons.YesNo) == DialogResult.Yes;
        }

        #region PaymentController events    
        private void onReaderEvent(ReaderEvent readerEvent)
        {
            log(string.Format("EVENT : {0}", readerEvent.ToString()));
        }

        private void onPaymentError(PaymentError error, string errorMsg)
        {
            log(string.Format("ERROR : {0} ({1})", error.ToString(), errorMsg ?? ""));
        }

        private void onTransactionStarted(String transactionID)
        {
            log(string.Format("TRANSACTION {0} STARTED", transactionID));
        }

        private void onPaymentFinished(PaymentResultContext result)
        {
            log(string.Format("PAYMENT FINISHED : " + Environment.NewLine
                + " ID : {0}" + Environment.NewLine
                + " Invoice : {1}" + Environment.NewLine
                + " ApprovalCode : {2}" + Environment.NewLine
                + " Amount : {3}" + Environment.NewLine
                + " DateTime : {4}" + Environment.NewLine
                + " PAN : {5}" + Environment.NewLine
                + " Terminal : {6}" + Environment.NewLine
                + " EMVdata : {7}" + Environment.NewLine
                + " RequiresSignature : {8}",
                result.TransactionItem.ID,
                result.TransactionItem.Invoice,
                result.TransactionItem.AcquirerApprovalCode,
                result.TransactionItem.Amount,
                result.TransactionItem.Date,
                result.TransactionItem.Card != null ? result.TransactionItem.Card.PANMasked : "null",
                result.TerminalName, result.EmvData,
                result.RequiresSignature));
            log(divider);
        }

        private void onReverseEvent(ReverseEvent reverseEvent, string message)
        {
            log(string.Format("REVERSE : {0}", message));
        }

        #endregion

        #region UI actions        
        private void btn_Start_Click(object sender, EventArgs e)
        {
            try
            {
                RadioButton checkedReader = gb_Reader.Controls.OfType<RadioButton>().FirstOrDefault(btn => btn.Checked);
                ReaderType readerType = (ReaderType)Enum.Parse(typeof(ReaderType), checkedReader.Text, true);

                PortInfo selectedPort = null;
                if (portInfos != null && portInfos.Count > 0 && cmb_Paired.SelectedIndex != -1)
                    selectedPort = portInfos[cmb_Paired.SelectedIndex];
                m_PaymentController.SetReaderType(readerType, (cb_Usb.Checked || selectedPort == null) ? null : selectedPort.portName);
            }
            catch (InvalidOperationException ex)
            {
                log(string.Format("ERROR : {0}", ex.Message));
            }

            //DEBUG
            //m_PaymentController.Logger = delegate (string s_log) { log(s_log, Color.Blue); };
            //

            m_PaymentController.Enable();
        }

        private void btn_Stop_Click(object sender, EventArgs e)
        {
            m_PaymentController.Disable();
        }

        private void btn_StartPayment_Click(object sender, EventArgs e)
        {
            startPayment();
        }

        private void btn_Reverse_Click(object sender, EventArgs e)
        {
            reversePayment();
        }

        private void btn_ClearLog_Click(object sender, EventArgs e)
        {
            edt_Log.Clear();
        }

        private void cb_Regular_CheckedChanged(object sender, EventArgs e)
        {
            gb_Regular.Enabled = cb_Regular.Checked;
        }

        private void cbl_RepeatType_SelectedIndexChanged(object sender, EventArgs e)
        {
            RepeatType repeatType = (RepeatType)cbl_RepeatType.SelectedIndex;

            var controls = gb_Regular.Controls;
            foreach (Control control in controls)
                control.Enabled = false;

            cbl_RepeatType.Enabled = true;
            cbl_End.Enabled = true;
            dtp_StartDate.Enabled = true;
            dtp_Time.Enabled = true;
            edt_Email.Enabled = true;
            edt_Phone.Enabled = true;
            lbl_Repeat.Enabled = true;
            lbl_End.Enabled = true;
            lbl_StartDate.Enabled = true;
            lbl_Time.Enabled = true;
            lbl_Email.Enabled = true;
            lbl_Phone.Enabled = true;

            switch (repeatType)
            {
                case RepeatType.DelayedOnce:
                    break;
                case RepeatType.Weekly:
                    cbl_DayOfWeek.Enabled = true;
                    lbl_DayOfWeek.Enabled = true;
                    break;
                case RepeatType.Monthly:
                    cbl_Day.Enabled = true;
                    lbl_Day.Enabled = true;
                    break;
                case RepeatType.Quarterly:
                    cbl_QMonth.Enabled = true;
                    cbl_Day.Enabled = true;
                    lbl_QMonth.Enabled = true;
                    lbl_Day.Enabled = true;
                    break;
                case RepeatType.Annual:
                    cbl_Month.Enabled = true;
                    cbl_Day.Enabled = true;
                    lbl_Month.Enabled = true;
                    lbl_Day.Enabled = true;
                    break;
                case RepeatType.ArbitraryDates:
                    edt_ArbitraryDays.Enabled = true;
                    dtp_StartDate.Enabled = false;
                    cbl_End.Enabled = false;
                    edt_RepeatCount.Enabled = false;
                    lbl_StartDate.Enabled = false;
                    lbl_ArbitraryDays.Enabled = true;
                    lbl_End.Enabled = false;
                    lbl_RepeatCount.Enabled = false;
                    break;

            }
        }

        private void cbl_End_EnabledChanged(object sender, EventArgs e)
        {
            EndType endType = (EndType)cbl_End.SelectedIndex;

            edt_RepeatCount.Enabled = cbl_End.Enabled ? endType == EndType.ByQuantity : false;
            dtp_EndDate.Enabled = cbl_End.Enabled ? endType == EndType.AtDay : false;
            lbl_RepeatCount.Enabled = cbl_End.Enabled ? endType == EndType.ByQuantity : false;
            lbl_EndDate.Enabled = cbl_End.Enabled ? endType == EndType.AtDay : false;
        }

        private void cbl_End_SelectedIndexChanged(object sender, EventArgs e)
        {
            EndType endType = (EndType)cbl_End.SelectedIndex;
            edt_RepeatCount.Enabled = endType == EndType.ByQuantity;
            lbl_RepeatCount.Enabled = endType == EndType.ByQuantity;
            dtp_EndDate.Enabled = endType == EndType.AtDay;
            lbl_EndDate.Enabled = endType == EndType.AtDay;
        }

        private void btn_Adjust_Click(object sender, EventArgs e)
        {
            adjustPayment();
        }

        private void btn_History_Click(object sender, EventArgs e)
        {
            getHistory();
        }

        private void cb_Product_CheckedChanged(object sender, EventArgs e)
        {
            gb_Product.Enabled = cb_Product.Checked;
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            m_PaymentController.Disable();
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            m_PaymentController.CancelPayment();
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            new Thread(() =>
            {
                try
                {
                    cmb_Paired.Invoke((MethodInvoker)delegate
                    {
                        cmb_Paired.Items.Clear();
                        cmb_Paired.Enabled = false;
                        cmb_Paired.Text = "Loading paired devices...";
                    });

                    ManagementObjectSearcher ManObjSearch = new ManagementObjectSearcher("Select * from Win32_SerialPort where PNPDeviceID like 'BTHENUM%'");
                    ManagementObjectCollection ManObjReturn = ManObjSearch.Get();

                    foreach (ManagementObject ManObj in ManObjReturn)
                    {
                        string portName = ManObj["DeviceID"].ToString();
                        string pnpDeviceID = ManObj["PNPDeviceID"].ToString();

                        Console.WriteLine(portName + " " + pnpDeviceID);

                        string macAddress;

                        try
                        {
                            int startIndex = pnpDeviceID.LastIndexOf('&') + 1;
                            int endIndex = pnpDeviceID.LastIndexOf('_');
                            macAddress = pnpDeviceID.Substring(startIndex, endIndex - startIndex);
                        }
                        catch
                        {
                            continue;
                        }

                        if (macAddress == "000000000000")
                        {
                            continue;
                        }

                        BluetoothAddress bluetoothAddress = BluetoothAddress.CreateFromBigEndian(hexToByteArray(macAddress));
                        BluetoothDeviceInfo bluetoothDeviceInfo = new BluetoothDeviceInfo(bluetoothAddress);
                        string deviceName = bluetoothDeviceInfo.DeviceName;

                        portInfos.Add(new PortInfo(portName, deviceName));
                        cmb_Paired.Invoke((MethodInvoker)delegate { cmb_Paired.Items.Add(deviceName); });
                    }

                    if (cmb_Paired.Items.Count == 0)
                    {
                        string[] ports = SerialPort.GetPortNames();
                        for (int i = 0; i < ports.Length; ++i)
                        {
                            portInfos.Add(new PortInfo(ports[i], ""));
                            cmb_Paired.Invoke((MethodInvoker)delegate { cmb_Paired.Items.Add(ports[i]); });
                        }
                    }

                    cmb_Paired.Invoke((MethodInvoker)delegate
                    {
                        if (cmb_Paired.Items.Count == 0)
                        {
                            cmb_Paired.Text = "Device not found";
                        }
                        else
                        {
                            cmb_Paired.Text = "";
                            cmb_Paired.Enabled = true;
                        }
                    });
                }
                catch { }
            }).Start();
        }

        #endregion

        public class PortInfo
        {
            public string portName { get; set; }
            public string deviceName { get; set; }

            public PortInfo(string portName, string deviceName)
            {
                this.portName = portName;
                this.deviceName = deviceName;
            }
        }

        private static byte[] hexToByteArray(string hex)
        {
            byte[] result = new byte[hex.Length / 2];
            for (int i = 0; i < result.Length; ++i)
            {
                result[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            }
            return result;
        }

        private void cb_Usb_CheckedChanged(object sender, EventArgs e)
        {
            if (cb_Usb.Checked)
            {
                lbl_PairedDevices.Enabled = false;
                cmb_Paired.Enabled = false;
            }
            else
            {
                lbl_PairedDevices.Enabled = true;
                if (portInfos != null && portInfos.Count > 0)
                    cmb_Paired.Enabled = true;
            }
        }
    }
}