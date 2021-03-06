﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using SocketTool.Core;
using System.Threading;
using System.Diagnostics;

namespace SocketTool
{
    public partial class ClientForm : Form, ISocketInfo
    {
        public ClientForm()
        {
            InitializeComponent();
            SocketInfo = new SocketInfo();

        }

        private static log4net.ILog logger = log4net.LogManager.GetLogger(typeof(ClientForm));  
        private IClient socketClient = new CommTcpClient();
        private Thread HeartOutgoingThread;
        private Thread SendOutgoingThread ;

        private int sendInterval = 0;
        private Boolean IsAutoSend;

        private Boolean continueSend = false;

        private string loginContent;
        private string heartContent;
        private string sendContent;

        private string errorMsg = "";

        public SocketInfo SocketInfo {get;set;}

        private void btnSend_Click(object sender, EventArgs e)
        {
            if (rbUdp.Checked) 
                socketClient = new CommUdpClient();

            socketClient.OnDataReceived += new ReceivedHandler(ListenMessage);
            socketClient.OnSocketError += new SocketErrorHandler(ListenErrorMessage);
            string ServerIP = this.txtIP.Text;
            errorMsg = "";
            if (string.IsNullOrEmpty(ServerIP))
                errorMsg += "请输入合法的IP地址";
            try
            {
                int Port = int.Parse(this.txtPort.Text);

                socketClient.Init(ServerIP, Port);
            }
            catch (Exception ex)
            {
                errorMsg += "请输入合法的端口";
            }
            loginContent = this.rtLoginData.Text;
            heartContent = this.rtHeartData.Text;
            sendContent = this.rtLoginData.Text;
            if (string.IsNullOrEmpty(sendContent))
                errorMsg += "请输入要发送的内容";
            
            if (cbAutoSend.Checked)
            {
                try
                {
                    sendInterval = int.Parse(txtInterval.Text) * 1000;
                }
                catch (Exception ex)
                {
                    errorMsg += "请输入整数的发送时间间隔";
                }
                
                IsAutoSend = true;
            }
            if (string.IsNullOrEmpty(errorMsg) == false)
            {
                MessageBox.Show(errorMsg);
                return;
            }
            continueSend = true;
            btnDisconnect.Enabled = true;
            btnSend.Enabled = IsAutoSend == false;

            HeartOutgoingThread = new Thread(new ThreadStart(HeartThreadFunc));
            HeartOutgoingThread.Start();

            SendOutgoingThread = new Thread(new ThreadStart(SendThreadFunc));
            SendOutgoingThread.Start();
            /**
            else
            {
                if (string.IsNullOrEmpty(errorMsg) == false)
                {
                    MessageBox.Show(errorMsg);
                    return;
                }
                byte[] data = System.Text.Encoding.Default.GetBytes(sendContent);
                try
                {
                    tcpClient.SendData(data);
                    btnDisconnect.Enabled = true;
                }
                catch (Exception ex)
                {
                    MessageBox.Show("发送数据出错，无法连接服务器");
                }
            }
             */
            
        }

        private void SendThreadFunc()
        {
            //发送登录数据包
            byte[] loginData = System.Text.Encoding.Default.GetBytes(loginContent);

            if (rbHex.Checked)
            {
                loginData = ParseUtil.ToByesByHex(loginContent);
            }
            socketClient.Send(loginData);

            //发送一般数据包
            while (continueSend)
            {
                byte[] data = System.Text.Encoding.Default.GetBytes(sendContent);

                if (rbHex.Checked)
                {
                    data = ParseUtil.ToByesByHex(sendContent);
                }

                try
                {
                    socketClient.Send(data);
                }
                catch (Exception ex)
                {
                    ListenMessage(0, "", ex.Message);
                    break;
                }
                if (IsAutoSend == false)
                    break;
                Thread.Sleep(sendInterval);

            }
        }

        private void HeartThreadFunc()
        {
            while (continueSend)
            {
                byte[] data = System.Text.Encoding.Default.GetBytes(heartContent);

                if (rbHex.Checked)
                {
                    data = ParseUtil.ToByesByHex(heartContent);
                }

                try
                {
                    socketClient.Send(data);
                }
                catch (Exception ex)
                {
                    ListenMessage(0, "", ex.Message);
                    break;
                }
                if (IsAutoSend == false)
                    break;
                Thread.Sleep(int.Parse(txtHeartTime.Text) * 1000);

            }
        }
        public void ListenErrorMessage(object o, SocketEventArgs e)
        {
            string errorMsg = "[" + e.ErrorCode + "]" + SocketUtil.DescrError(e.ErrorCode);

            ListenMessage((int)o, "Socket错误", errorMsg);

        }

        private void ListenMessage(object ID, string type, string msg)
        {
            if (PacketView.InvokeRequired)
            {
                try
                {
                    MsgHandler d = new MsgHandler(ListenMessage);
                    this.Invoke(d, new object[] {0,type, msg});
                }
                catch (System.Exception ex)
                {
                    logger.Error(ex.Message);
                    logger.Error(ex.StackTrace);
                }
            }
            else
            {
                if (type == "Socket错误")
                {
                    continueSend = false;
                    btnDisconnect.Enabled = false;
                    btnSend.Enabled = true;
                }
                if (PacketView.Items.Count > 200)
                    PacketView.Items.Clear();


                ListViewItem item = PacketView.Items.Insert(0, "" + PacketView.Items.Count);

                //int length = e.Data.Length;
                string strDate = DateTime.Now.ToString("HH:mm:ss");
                item.SubItems.Add(strDate);
                item.SubItems.Add(msg);
                //item.SubItems.Add("" + length);
            }

        }

        public void ListenMessage(object o, ReceivedEventArgs e)
        {
            if (PacketView.InvokeRequired)
            {
                try
                {
                    ReceivedHandler d = new ReceivedHandler(ListenMessage);
                    this.Invoke(d, new object[] {o, e });
                }
                catch (System.Exception ex)
                {
                    logger.Error(ex.Message);
                    logger.Error(ex.StackTrace);
                }
            }
            else
            {
                if (PacketView.Items.Count > 200)
                    PacketView.Items.Clear();

                ListViewItem item = PacketView.Items.Insert(0, "" + PacketView.Items.Count);

                int length = e.Data.Length;
                string strDate = DateTime.Now.ToString("HH:mm:ss");
                item.SubItems.Add(strDate);
                string msg = ParseUtil.ParseString(e.Data, length);
                if (rbHex.Checked)
                    msg = ParseUtil.ToHexString(e.Data, length);
                item.SubItems.Add(msg);
                item.SubItems.Add("" + length);

                if (cbLog.Checked)
                {
                    logger.Info(e.RemoteHost.ToString() + " " + msg);
                }
                //item.SubItems.Add("" + msg.MsgContentDesc);
            }
        }

        private void cbAutoSend_CheckedChanged(object sender, EventArgs e)
        {
            this.txtInterval.Enabled = cbAutoSend.Checked;
        }

        private void btnDisconnect_Click(object sender, EventArgs e)
        {
            continueSend = false;
            try
            {
                HeartOutgoingThread.Abort();
                SendOutgoingThread.Abort();
                if (socketClient != null)
                    socketClient.Close();
            }
            catch (Exception ex)
            {
            }

            this.btnSend.Enabled = true;
        }

        private void ClientForm_FormClosing(object sender, FormClosingEventArgs e)
        {

            SocketInfo.ServerIp = this.txtIP.Text;
            try
            {
                SocketInfo.Port = int.Parse(this.txtPort.Text);
            }
            catch (System.Exception ex)
            {

            }

            SocketInfo.Protocol = rbTcp.Checked ? "Tcp" : "Udp";
            SocketInfo.Format = rbAscII.Checked ? "AscII" : "Hex";
            SocketInfo.Type = "Client";
            SocketInfo.ServerIp = this.txtIP.Text;
            SocketInfo.Data = this.rtSendData.Text;

            SocketInfo.IsAuto = cbAutoSend.Checked;
            try
            {
                SocketInfo.Port = int.Parse(this.txtPort.Text);
            }
            catch (Exception ex)
            {
                //errorMsg += "请输入合法的端口";
            }
            /*
            continueSend = false;
            try
            {
                HeartOutgoingThread.Abort();
                SendOutgoingThread.Abort();
                if (socketClient != null)
                    socketClient.Close();
            }
            catch (Exception ex)
            {
            }

            this.btnSend.Enabled = true;
             */

        }

        private void btnClearLog_Click(object sender, EventArgs e)
        {
            this.PacketView.Clear();
        }

        private void ClientForm_Load(object sender, EventArgs e)
        {
            this.txtIP.Text = SocketInfo.ServerIp;
            rbTcp.Checked = SocketInfo.Protocol == "Tcp";
            rbUdp.Checked = SocketInfo.Protocol != "Tcp";
            rbAscII.Checked = SocketInfo.Format == "AscII";
            rbHex.Checked = SocketInfo.Format != "AscII";
            this.txtPort.Text = "" + SocketInfo.Port;
            this.rtSendData.Text = SocketInfo.Data;

            cbAutoSend.Checked = SocketInfo.IsAuto;

        }

        private void btnOpenLog_Click(object sender, EventArgs e)
        {
            Process.Start("notepad.exe", "client.log");
        }

        private void panel1_Paint(object sender, PaintEventArgs e)
        {

        }
    }
}
