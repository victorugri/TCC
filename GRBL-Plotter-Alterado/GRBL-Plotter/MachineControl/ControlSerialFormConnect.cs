/*  GRBL-Plotter. Another GCode sender for GRBL.
    This file is part of the GRBL-Plotter application.
   
    Copyright (C) 2015-2022 Sven Hasemann contact: svenhb@web.de

    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.
*/

/* 2022-01-09 split file for selection of serial or ethernet
*/

using System;
using System.IO;
using System.IO.Ports;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace GrblPlotter
{
    public partial class ControlSerialForm : Form        // Form can be loaded twice!!! COM1, COM2
    {

        public bool IsConnectedToGrbl()
        {
            if (!useEthernet) { return serialPort.IsOpen; }
            else { return Connected; }
        }

        /***** ethernet preperations *****/

        private NetworkStream Connection;
        private TcpClient ClientEthernet;
        private bool Connected = false;
        private bool UseSocket = true;
        private StreamReader reader;
        DateTime horarioInicial;

        TimeSpan[] vetorTempoBloco = new TimeSpan[3];

        string[] vetorPosicaoBloco = new string[3];
        //     StreamWriter writer;

        public void ConnectToGrbl()
        { ConnectToGrbl(null, null); }
        public void ConnectToGrbl(object sender, EventArgs e)//, bool showMessageBox = true)
        {
            bool showMessageBox = false;	//true;
            rxErrorCount = 0;
			tryDoSerialConnection = true;
            rtbLog.Clear();
            Grbl.isMarlin = isMarlin = Properties.Settings.Default.ctrlConnectMarlin;
            if (isMarlin)
                AddToLog("Force connection to Marlin");

            useEthernet = CbEthernetUse.Checked;
            if (!useEthernet)
            {
                AddToLog("\nTry to connect to serial " + cbPort.Text + " @ " + cbBaud.Text);
				EventCollector.SetCommunication("C" + cbPort.Text);
                Application.DoEvents();
                OpenPortSerial();
                if (serialPort.IsOpen)
                {
                    CbEthernetUse.Enabled = false;
                    Connected = true;
                    tryDoSerialConnection = false;
                }
            }
            else
            {
                try
                {
                    Logger.Info("==== Connecting to {0}:{1} ====", TbEthernetIP.Text, TbEthernetPort.Text);
                    AddToLog("\nTry to connect via Ethernet - Telnet\nConnect to " + TbEthernetIP.Text + ":" + TbEthernetPort.Text + "\nIf fails, it takes up to 20 sec. to response!");
					EventCollector.SetCommunication("CEther");
					timerSerial.Interval = 1000;
                    timerSerial.Start();

                    if (int.TryParse(TbEthernetPort.Text, out int port))
                    {

                        // https://docs.microsoft.com/de-de/dotnet/framework/network-programming/asynchronous-client-socket-example
                     //   if (UseSocket)
                     //   { }
                        // Telnet
                    //    else
                        {
                            if ((port >= 0) && (port <= 65535))
                            {
                                BtnOpenPortEthernet.Text = Localization.GetString("serialClose");
                                BtnOpenPortEthernet.Enabled = false;
                                CbEthernetUse.Enabled = false;
                                Application.DoEvents();
                                ClientEthernet = new TcpClient(TbEthernetIP.Text, port);        // Telnet
                                Connected = true;
                                Connection = ClientEthernet.GetStream();
                                tryDoSerialConnection = false;

                                SaveSettings();
                                reader = new StreamReader(Connection, System.Text.Encoding.ASCII);
                                AddToLog("Connect via Ethernet - Telnet " + TbEthernetIP.Text + ":" + TbEthernetPort.Text);

                                ConnectionSucceed("Connect to Ethernet " + TbEthernetIP.Text + ":" + TbEthernetPort.Text);

                                timerSerial.Interval = 500;             // timerReload;

                                CbEthernetUse.Enabled = false;
                                BtnOpenPortEthernet.Enabled = true;
                                Application.DoEvents();
                            }
                            else
                            {
                                countMinimizeForm = 0;
                                string msg = string.Format("Port number must be between 0 and 65535: {0}");
                                Logger.Error(msg);
                                AddToLog(msg);
                                if (showMessageBox) MessageBox.Show(msg, "Error");
                            }
                        }
                    }
                    else
                    {
						countMinimizeForm = 0;
                        string msg = string.Format("Port is not a valid number: {0}", TbEthernetPort.Text);
                        Logger.Error(msg);
                        AddToLog(msg);
                        if (showMessageBox) MessageBox.Show(msg, "Error");
                    }
                }
                catch (ArgumentNullException)
                {
					countMinimizeForm = 0;
                    string msg = "ArgumentNullException - Invalid address or port.\nWrong port? Telnet on esp expects port 23.";
                    Logger.Error(msg);
                    AddToLog(msg);
                    if (showMessageBox) MessageBox.Show(msg, "Error");
                    CbEthernetUse.Enabled = true;
                    BtnOpenPortEthernet.Enabled = true;
                    BtnOpenPortEthernet.Text = Localization.GetString("serialOpen");
                    Connected = false;
                }
                catch (SocketException)
                {
					countMinimizeForm = 0;
                    string msg = "SocketException - Connection failure.\nWrong port? Telnet on esp expects port 23.";
                    Logger.Error(msg);
                    AddToLog(msg);
                    if (showMessageBox) MessageBox.Show(msg, "Error");
                    CbEthernetUse.Enabled = true;
                    BtnOpenPortEthernet.Enabled = true;
                    BtnOpenPortEthernet.Text = Localization.GetString("serialOpen");
                    Connected = false;
                }
                tryDoSerialConnection = false;
            }
            UpdateControls();
        }
		
        public void DisconnectFromGrbl(object sender, EventArgs e)
        {
			tryDoSerialConnection = false;
            Connected = false;
         //   reader = null;
            //     writer = null;
            CbEthernetUse.Enabled = true;

			EventCollector.SetCommunication("CDisc");
            useEthernet = CbEthernetUse.Checked;
            if (!useEthernet)
            {
                ClosePortSerial();
				BtnOpenPortSerial.Text = Localization.GetString("serialOpen");
            }
            else
            {
                if (Connection != null)
                {
                    Connection.Flush();
                    Connection.Close();
                    Connection.Dispose();
                    reader.Close();
                    ClientEthernet.Close();
                    AddToLog("==== Disconnected from Ethernet ====");
                }
                timerSerial.Interval = 1000;
                //SaveSettings();
                Connection = null;
                CbEthernetUse.Enabled = true;
                BtnOpenPortEthernet.Enabled = true;
                BtnOpenPortEthernet.Text = Localization.GetString("serialOpen");
            }
            if (iamSerial == 1) { Grbl.isConnected = SerialPortOpen = IsConnectedToGrbl(); }
            OnRaisePosEvent(new PosEventArgs(posWork, posMachine, GrblState.unknown, machineState, mParserState, ""));// lastCmd));
            UpdateControls();
        }
		
		private void ConnectionSucceed(string msg)
		{
			BtnOpenPortSerial.Text = Localization.GetString("serialClose");  // "Close";
			isDataProcessing = true;
			if (iamSerial == 1)
			{	Grbl.isConnected = IsConnectedToGrbl();
				Grbl.lastMessage = msg;
				Grbl.Clear();			// reset internal grbl variables
			}

			timerSerial.Interval = Grbl.pollInterval;       		// timerReload;
			countMissingStatusReport = (int)(2000 / timerSerial.Interval);

            if (Properties.Settings.Default.serialMinimize)
                countMinimizeForm = (int)(2000 / timerSerial.Interval);     // minimize window after 3 sec.

            timerSerial.Enabled = true;
			serialPortError = false;

			countPreventOutput = 0; countPreventEvent = 0;
			IsHeightProbing = false;
			if (Grbl.grblSimulate)
			{
				Grbl.grblSimulate = false;
				AddToLog("* Stop simulation\r\n");
			}
			GrblReset(false);   		// reset controller, don't savePos, wait for reset response

			OnRaisePosEvent(new PosEventArgs(posWork, posMachine, GrblState.unknown, machineState, mParserState, ""));// lastCmd));					
		}

        public string[] connectToArduinoUno()
        {
            horarioInicial = DateTime.Now;
            string selectedPort = "COM4";
            serialPort = new SerialPort(selectedPort, 115200, Parity.None, 8, StopBits.One);
            serialPort.Open();
            //textBox1.Text = "CONECTADO";
            //button1.Text = "Disconnect";
            //enableControls();

            var teste = serialPort.ReadLine();
            vetorTempoBloco[0] = (DateTime.Now - horarioInicial);
            //textBox1.Text = vetorTempoBloco[0].ToString();
            var teste2 = serialPort.ReadLine();
            vetorTempoBloco[1] = (DateTime.Now - horarioInicial);
            //textBox2.Text = vetorTempoBloco[1].ToString();
            var teste3 = serialPort.ReadLine();
            vetorTempoBloco[2] = (DateTime.Now - horarioInicial);
            //textBox3.Text = vetorTempoBloco[2].ToString();

            converterTempoEmPosicao();

            return vetorPosicaoBloco;
        }

        private void converterTempoEmPosicao()
        {
            //for (int i = 0; i < 3; i++)
            //{
            //    //if (vetorTempoBloco[0].Seconds < 22)
            //    //    textBox1.Text = "Erro, leitura muito rapida do primeiro bloco!";
            //    //posicao 1x1
            //    if (22 <= vetorTempoBloco[i].Seconds && vetorTempoBloco[i].Seconds <= 24)
            //        vetorPosicaoBloco[i] = "1x1";
            //    //posicao 1x2
            //    else if (25 <= vetorTempoBloco[i].Seconds && vetorTempoBloco[i].Seconds <= 27)
            //        vetorPosicaoBloco[i] = "1x2";
            //    //posicao 1x3
            //    else if (28 <= vetorTempoBloco[i].Seconds && vetorTempoBloco[i].Seconds <= 30)
            //        vetorPosicaoBloco[i] = "1x3";
            //    //posicao 2x1
            //    else if (40 <= vetorTempoBloco[i].Seconds && vetorTempoBloco[i].Seconds <= 42)
            //        vetorPosicaoBloco[i] = "2x3";
            //    //posicao 2x2
            //    else if (43 <= vetorTempoBloco[i].Seconds && vetorTempoBloco[i].Seconds <= 45)
            //        vetorPosicaoBloco[i] = "2x2";
            //    //posicao 2x3
            //    else if (46 <= vetorTempoBloco[i].Seconds && vetorTempoBloco[i].Seconds <= 48)
            //        vetorPosicaoBloco[i] = "2x1";
            //    //posicao 3x1
            //    else if (58 <= vetorTempoBloco[i].Seconds && vetorTempoBloco[i].Seconds <= 60)
            //        vetorPosicaoBloco[i] = "3x1";
            //    //posicao 3x2
            //    else if (61 <= vetorTempoBloco[i].Seconds && vetorTempoBloco[i].Seconds <= 63)
            //        vetorPosicaoBloco[i] = "3x2";
            //    //posicao 2x3
            //    else if (64 <= vetorTempoBloco[i].Seconds && vetorTempoBloco[i].Seconds <= 66)
            //        vetorPosicaoBloco[i] = "3x3";
            //}

            //temporario p teste
            for (int i = 0; i < 3; i++)
            {
                vetorPosicaoBloco[i] = vetorTempoBloco[i].TotalSeconds.ToString();
            }

        }

    }
}
