using Microsoft.Win32; // for the registry table
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Net; // for the ip address
using System.Runtime.InteropServices; // for the P/Invoke
using System.Text;
using System.Windows.Forms;
using System.Diagnostics;

namespace WindowsApplication1
{
	public partial class Form1 : Form
	{

		System.IntPtr hDMTDll; // handle of a loaded DLL
        private Stopwatch stopwatch = new Stopwatch();

        delegate void DelegateClose(int conn_num); // function pointer for disconnection
		
		 // About .Net P/Invoke:

		 // [DllImport("XXX.dll", CharSet = CharSet.Auto)] 
		 // static extern int ABC(int a , int b);
		  
		 // indicates that "ABC" function is imported from XXX.dll
		 // XXX.dll exports a function of the same name with "ABC"
		 // the return type and the parameter's data type of "ABC" 
		 // must be identical with the function exported from XXX.dll
		 // and the CharSet = CharSet.Auto causes the CLR 
		 // to use the appropriate character set based on the host OS   
		[DllImport("kernel32.dll", CharSet = CharSet.Auto)]
		static extern IntPtr LoadLibrary(string dllPath);
		[DllImport("kernel32.dll", CharSet = CharSet.Auto)]
		static extern bool FreeLibrary(IntPtr hDll);

		// Data Access
		[DllImport("DMT.dll", CharSet = CharSet.Auto)] 
		static extern int  RequestData(int comm_type, int conn_num, int slave_addr, int func_code, byte[] sendbuf, int sendlen);
		[DllImport("DMT.dll", CharSet = CharSet.Auto)] 
		static extern int  ResponseData(int comm_type, int conn_num, ref int slave_addr, ref int func_code, byte[] recvbuf);

		// Serial Communication
		[DllImport("DMT.dll", CharSet = CharSet.Auto)] 
		static extern int OpenModbusSerial(int conn_num, int baud_rate, int data_len, char parity, int stop_bits, int modbus_mode);
		[DllImport("DMT.dll", CharSet = CharSet.Auto)] 
		static extern void CloseSerial(int conn_num);
		[DllImport("DMT.dll", CharSet = CharSet.Auto)] 
		static extern int GetLastSerialErr();
		[DllImport("DMT.dll", CharSet = CharSet.Auto)] 
		static extern void ResetSerialErr();

		// Socket Communication
		[DllImport("DMT.dll", CharSet = CharSet.Auto)] 
		static extern int OpenModbusTCPSocket(int conn_num, int ipaddr);
		[DllImport("DMT.dll", CharSet = CharSet.Auto)] 
		static extern void CloseSocket(int conn_num);
		[DllImport("DMT.dll", CharSet = CharSet.Auto)] 
		static extern int GetLastSocketErr();
		[DllImport("DMT.dll", CharSet = CharSet.Auto)] 
		static extern void ResetSocketErr();
		[DllImport("DMT.dll", CharSet = CharSet.Auto)] 
		static extern int ReadSelect(int conn_num, int millisecs);

		// MODBUS Address Calculation
		[DllImport("DMT.dll", CharSet = CharSet.Auto)] 
		static extern int DevToAddrW(string series, string device, int qty);
		
		// Wrapped MODBUS Funcion : 0x01
		[DllImport("DMT.dll", CharSet = CharSet.Auto)]
		static extern int ReadCoilsW(int comm_type, int conn_num, int slave_addr, int dev_addr, int qty, UInt32[] data_r, StringBuilder req, StringBuilder res);
		
		// Wrapped MODBUS Funcion : 0x02
		[DllImport("DMT.dll", CharSet = CharSet.Auto)]
		static extern int ReadInputsW(int comm_type, int conn_num, int slave_addr, int dev_addr, int qty, UInt32[] data_r, StringBuilder req, StringBuilder res);
		
		// Wrapped MODBUS Funcion : 0x03
		[DllImport("DMT.dll", CharSet = CharSet.Auto)]
		static extern int ReadHoldRegsW(int comm_type, int conn_num, int slave_addr, int dev_addr, int qty, UInt32[] data_r, StringBuilder req, StringBuilder res);
		[DllImport("DMT.dll", CharSet = CharSet.Auto)]
		static extern int ReadHoldRegs32W(int comm_type, int conn_num, int slave_addr, int dev_addr, int qty, UInt32[] data_r, StringBuilder req, StringBuilder res);        
		
		// Wrapped MODBUS Funcion : 0x04
		[DllImport("DMT.dll", CharSet = CharSet.Auto)]
		static extern int ReadInputRegsW(int comm_type, int conn_num, int slave_addr, int dev_addr, int qty, UInt32[] data_r, StringBuilder req, StringBuilder res);        

		// Wrapped MODBUS Funcion : 0x05		   
		[DllImport("DMT.dll", CharSet = CharSet.Auto)]
		static extern int WriteSingleCoilW(int comm_type, int conn_num, int slave_addr, int dev_addr, UInt32 data_w, StringBuilder req, StringBuilder res);

		// Wrapped MODBUS Funcion : 0x06
		[DllImport("DMT.dll", CharSet = CharSet.Auto)]
		static extern int WriteSingleRegW(int comm_type, int conn_num, int slave_addr, int dev_addr, UInt32 data_w, StringBuilder req, StringBuilder res);
		[DllImport("DMT.dll", CharSet = CharSet.Auto)]
		static extern int WriteSingleReg32W(int comm_type, int conn_num, int slave_addr, int dev_addr, UInt32 data_w, StringBuilder req, StringBuilder res);
	   
		// Wrapped MODBUS Funcion : 0x0F
		[DllImport("DMT.dll", CharSet = CharSet.Auto)]
		static extern int WriteMultiCoilsW(int comm_type, int conn_num, int slave_addr, int dev_addr, int qty, UInt32[] data_w, StringBuilder req, StringBuilder res);

		// Wrapped MODBUS Funcion : 0x10
		[DllImport("DMT.dll", CharSet = CharSet.Auto)]
		static extern int WriteMultiRegsW(int comm_type, int conn_num, int slave_addr, int dev_addr, int qty, UInt32[] data_w, StringBuilder req, StringBuilder res);
		[DllImport("DMT.dll", CharSet = CharSet.Auto)]
		static extern int WriteMultiRegs32W(int comm_type, int conn_num, int slave_addr, int dev_addr, int qty, UInt32[] data_w, StringBuilder req, StringBuilder res);
		  
		public Form1()
		{
			InitializeComponent();
            
        }

		private void Form1_Load(object sender, EventArgs e)
		{
			
            //string path = System.Environment.CurrentDirectory;

            //path = path.Remove(path.Length - 9);
            //path = path.Replace("\\", "\\\\");
            //path = path.Insert(path.Length, "DMT.dll"); // obtain the relative path where the DMT.dll resides

            hDMTDll = LoadLibrary("DMT.dll"); // explicitly link to DMT.dll 

			
			RegistryKey MyRegKey = Registry.LocalMachine.OpenSubKey("HARDWARE\\DEVICEMAP\\SERIALCOMM");            
			foreach (string valueName in MyRegKey.GetValueNames())
			{
				string a = MyRegKey.GetValue(valueName).ToString();
				ComPort.Items.Add(MyRegKey.GetValue(valueName).ToString()); // list out all the serial port on the local machine
			}

			Product.SelectedIndex = 0;
			ComPort.SelectedIndex = 0;
			
			IPAddr.Text = "192.168.1.5";

			QMode.SelectedIndex = 0;
			FuncCode.SelectedIndex = 0;
		}
		

		private void Product_SelectedIndexChanged(object sender, EventArgs e)
		{
			switch (Product.SelectedIndex)
			{
				case 0: // RTU-EN01
				case 1: // DVP-EN01/DVP-12SE
					CommType.SelectedIndex = 1;
					break;

				case 2: // DVP PLC
					CommType.SelectedIndex = 0;
					break;

				case 3: // AH PLC
                case 4: // AS PLC
                case 5: // 15MC/50MC
					CommType.SelectedIndex = 1;
					break;

				default:
					break;
			}

            ChangeDeviceName(Product.SelectedIndex);
		}

		private void CommType_SelectedIndexChanged(object sender, EventArgs e)
		{
			switch (CommType.SelectedIndex)
			{
				case 0: // RS-232
					MBMode.SelectedIndex = 0;
					MBMode.Enabled = true;
					ComPort.Enabled = true;
					IPAddr.Enabled = false;
					break;

				case 1: // Ethernet
					MBMode.SelectedIndex = 1;
					MBMode.Enabled = false;
					ComPort.Enabled = false;
					IPAddr.Enabled = true;
					break;
			}
		}

		private void QMode_SelectedIndexChanged(object sender, EventArgs e)
		{
			switch (QMode.SelectedIndex)
			{
				case 0:
					FuncCode.Enabled = false;
					ReqData.ReadOnly = false;
					SlavAddr.Enabled = false;
					DevName.Enabled = false;
					DataFromDev.Enabled = false;
					DataToDev.Enabled = false;
					break;
				case 1:
					FuncCode.Enabled = true;
					ReqData.ReadOnly = true;
					SlavAddr.Enabled = true;
					DevName.Enabled = true;
					DataFromDev.Enabled = true;
					DataToDev.Enabled = true;
					break;
			}
		}

		private void FuncCode_SelectedIndexChanged(object sender, EventArgs e)
		{
			if (FuncCode.SelectedIndex < 5) // Read Functions
				DataToDev.ReadOnly = true;
			else // Write Functions
				DataToDev.ReadOnly = false;
		}

		private void Form1_FormClosing(object sender, FormClosingEventArgs e)
		{
			FreeLibrary(hDMTDll);
		}

		private void button1_Click_1(object sender, EventArgs e)
		{
			string strReq = "";
			string strRes = "";

			ActStatus.Text = "";

			DelegateClose CloseModbus = new DelegateClose(CloseSerial); // function pointer for disconnection

			int conn_num = 0;

			int baud_rate = 9600; // fiixed
			int data_len = 7;
			char parity = 'E';
			int stop_bits = 1; // fixed 

			int modbus_mode = 1; // 1:ASCII , 2:RTU
			int status = 0;
			int comm_type = 0; // 0:RS-232 , 1:Ethernet
     
            // 声明并初始化一个 double 类型的 List
            List<double> actTime = new List<double>(new double[10000]);


            switch (MBMode.SelectedIndex) // update communication params
			{
				case 0: //ASCII
					data_len = 7;
					parity = 'E';
					modbus_mode = 1;
					break;

				case 1: // RTU
					data_len = 8;
					parity = 'N';
					modbus_mode = 2;
					break;
			}

			switch (CommType.SelectedIndex) // build the connection
			{
				case 0: // RS-232
					comm_type = 0;
					conn_num = Convert.ToInt32(ComPort.Text.Replace("COM", ""));
					CloseModbus = CloseSerial;

					status = OpenModbusSerial(conn_num, baud_rate, data_len, parity, stop_bits, modbus_mode);
					if (status == -1)
					{
						ActStatus.Text = "Serial Connection Failed";
						return;
					}
					break;

				case 1: // Ethernet
					comm_type = 1;
					conn_num = 0;
					CloseModbus = CloseSocket;

					IPAddress ipaddress = IPAddress.Parse(IPAddr.Text);
					int ip = BitConverter.ToInt32(ipaddress.GetAddressBytes(), 0); // same as inet_addr() 

					status = OpenModbusTCPSocket(conn_num, ip);
					if (status == -1)
					{
						ActStatus.Text = "Socket Connection Failed";
						return;
					}
					break;
			}

			if (QMode.SelectedIndex == 0)
			{
				byte[] sendbuf = new byte[1024];
				byte[] recvbuf = new byte[1024];
				int modbus_addr = 0;
				int modbus_func = 0;
				int modbus_addr_ret = 0;
				int modbus_func_ret = 0;
				int sendlen = 0;
				int i,j = 0;

				strReq = ReqData.Text;
				string strValid = "0123456789ABCDEF";
				

                /***************************************************
				 ex: // input verification
					 string str1 = "0123456789ABCDEF";
					 string str2 = "ax";
					 int num = str1.IndexOf(str2.ToUpper()[0]); // num = 10
					 num = str1.IndexOf(str2.ToUpper()[1]); // num = -1 : illegal char
				***************************************************/

                if (strReq.Length < 4) // at least slave address and function code
				{
					ActStatus.Text = "Invalid Modbus Data";
					CloseModbus(conn_num);
					return;
				}

				if (strReq.Length % 2 != 0) // input data must be even number
				{
					ActStatus.Text = "Modbus Data Must Be Even Number";
					CloseModbus(conn_num);
					return;
				}

				for (i = 0; i < strReq.Length; i++) // input data verification
				{
					if (strValid.IndexOf(strReq.ToUpper()[i]) == -1)
					{
						ActStatus.Text = "Invalid Modbus Data";
						CloseModbus(conn_num);
						return;
					}
				}

				for (i = 0; i <= strReq.Length - 2; i += 2) // trans data into bytes and put it into sendbuf
				{
					string strConvert = strReq[i].ToString() + strReq[i + 1].ToString();
					if (i == 0)
						modbus_addr = Convert.ToByte(strConvert, 16);
					else if (i == 2)
						modbus_func = Convert.ToByte(strConvert, 16);
					else
					{
						sendbuf[sendlen] = Convert.ToByte(strConvert, 16);
						sendlen++;
					}
				}
				for (j = 0; j < 10000; j++) // clear the rest of sendbuf
				{
                    stopwatch.Reset();
                    stopwatch.Start();
                    int req = RequestData(comm_type, conn_num, modbus_addr, modbus_func, sendbuf, sendlen);  // modbus request
                    if (req == -1)
                    {
                        ActStatus.Text = "Request Failed";
                        CloseModbus(conn_num);
                        return;
                    }
                    int res = ResponseData(comm_type, conn_num, ref modbus_addr_ret, ref modbus_func_ret, recvbuf);  // modbus response
                    stopwatch.Stop();
					actTime.Add(stopwatch.Elapsed.TotalMilliseconds);
                    if (res > 0)
                    {
                        strRes += modbus_addr_ret.ToString("X2");
                        strRes += modbus_func_ret.ToString("X2");

                        switch (modbus_func_ret)
                        {
                            case 0x01:
                            case 0x02:
                            case 0x03:
                            case 0x04:
                            case 0x11:
                            case 0x17:
                                strRes += res.ToString("X2");
                                break;
                        }

                        for (i = 0; i < res; i++) // recover a string from recvbuf
                        {
                            strRes += recvbuf[i].ToString("X2");
                        }

                        if ((modbus_func_ret & 0x80) == 0x80)
                            ActStatus.Text = "Request Failed";
                        else
                            ActStatus.Text = "Request Done";
                        ResData.Text = strRes;
                    }
                    else
                    {
                        ActStatus.Text = "No Data Received";
                        ResData.Text = "";
                    }
                }
                   
				
			}

			else
			{
				ReqData.Text = "";
				DataFromDev.Text = "0";

				StringBuilder req = new StringBuilder(1024);
				StringBuilder res = new StringBuilder(1024);

				UInt32[] data_from_dev = new UInt32[1];
				data_from_dev[0] = 0;

				UInt32[] data_to_dev = new UInt32[1];
				data_to_dev[0] = Convert.ToUInt32(DataToDev.Text);
				
				string strProduct = "";
				switch(Product.SelectedIndex)
				{
				case 0: // RTU-Series
				strProduct = "RTU";
				break;

				case 1: // DVP-Series
				case 2:
				strProduct = "DVP";
				break;

				case 3: // AH-Series
				strProduct = "AH";
				break;

                case 4: // AS-Series
                strProduct = "AS";
                break;

                case 5: // 15MC/50MC
                strProduct = "MC";
                break;
				}

				string strDev = DevName.Text;

				int slave_addr = Convert.ToInt32(SlavAddr.Text);
				int dev_qty = Convert.ToInt32(DevQty.Text);
				int addr = DevToAddrW(strProduct,strDev,dev_qty);
				
				if (addr == -1)
					ActStatus.Text = "Invalid Product of Series";
				else if (addr == -2)
					ActStatus.Text = "Invalid Device";
				else if (addr == -3)
					ActStatus.Text = "Device With Such Quantity Is Out Of Valid Range";
				else
				{
					int ret = 0;
					switch(FuncCode.SelectedIndex)
					{
					case 0:
						ret = ReadCoilsW(comm_type,conn_num,slave_addr,addr,dev_qty, data_from_dev,req,res);
					break;

					case 1:
						ret = ReadInputsW(comm_type,conn_num,slave_addr,addr,dev_qty, data_from_dev,req,res);
					break;

					case 2:
						ret = ReadHoldRegsW(comm_type,conn_num,slave_addr,addr,dev_qty,data_from_dev,req,res);
					break;

					case 3:
						ret = ReadHoldRegs32W(comm_type,conn_num,slave_addr,addr,dev_qty,data_from_dev,req,res);
					break;

					case 4:
						ret = ReadInputRegsW(comm_type,conn_num,slave_addr,addr,dev_qty,data_from_dev,req,res);
					break;

					case 5:
						ret = WriteSingleCoilW(comm_type,conn_num,slave_addr,addr,data_to_dev[0],req,res);
					break;

					case 6:
						ret = WriteSingleRegW(comm_type, conn_num, slave_addr, addr, data_to_dev[0], req, res);
					break;

					case 7:
						ret = WriteSingleReg32W(comm_type, conn_num, slave_addr, addr, data_to_dev[0], req, res);
					break;

					case 8:
						ret = WriteMultiCoilsW(comm_type,conn_num,slave_addr,addr,dev_qty,data_to_dev,req,res);
					break;

					case 9:
						ret = WriteMultiRegsW(comm_type,conn_num,slave_addr,addr,dev_qty,data_to_dev,req,res);
					break;

					case 10:
						ret = WriteMultiRegs32W(comm_type,conn_num,slave_addr,addr,dev_qty,data_to_dev,req,res);
					break;

					}

					ReqData.Text = req.ToString();
					ResData.Text = res.ToString();

					switch(ret)
					{
					case -1:
						ActStatus.Text = "Request Failed";
					break;

					default:
						{
							ActStatus.Text = "Request Done";
							if (FuncCode.SelectedIndex < 5)
								DataFromDev.Text = data_from_dev[0].ToString();
							break;
						}
					}
				}

			}
            actTime.Sort();
            actTime.Reverse();
            textBox1.Text = " (" + actTime[0].ToString() + " ms)";

            CloseModbus(conn_num);
		}

        private void ChangeDeviceName(int productIndex)
        {
            if (productIndex == 5)
                label17.Text = "(%IX0.7, %IW0, %QX127.7, %QW63, %MW32767)";
            else
                label17.Text = "(X0.15, Y0 , M73 , D1367)";
        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {

        }
    }


	
}
