using System;
using System.IO;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using System.Windows.Forms;
using System.Threading.Tasks;

using librpc;
using Be.Windows.Forms;

namespace PS4_Elf_Loader {
    public partial class Form1 : Form {
        public static PS4RPC PS4 = null;
        private string selectedProcess = null;
        private ProcessList pList = null;
    
        //Memory Viewer
        private static byte[] MemoryData = null;

        //Socket Listener
        private Task socketListener;
        private bool listenerOn = false;
        private static Socket ServerListener; 

        public Form1() {
            InitializeComponent();
            textBox1.Text = Properties.Settings.Default.PS4IP;
            comboBox2.Text = Properties.Settings.Default.PS4Version;
            hexBox1.ByteProvider = new DynamicByteProvider(new byte[0x1000]);
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e) {
            Properties.Settings.Default.PS4IP = textBox1.Text;
            Properties.Settings.Default.PS4Version = comboBox2.Text;
            Properties.Settings.Default.Save();
        }

        private void SendPayload(string IP, byte[] bytes, int port) {
            Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            socket.Connect(new IPEndPoint(IPAddress.Parse(IP), port));
            socket.Send(bytes);
            socket.Close();
        }

        private void button2_Click(object sender, EventArgs e) {
            try {
                label1.Text = "Sending Payload...";
                byte[] payload = File.ReadAllBytes(@"" + comboBox2.Text + "/payload.bin");
                SendPayload(textBox1.Text, payload, 9020);
                Thread.Sleep(1000);
                label1.Text = "Sending RPC...";
                byte[] jkpatch = File.ReadAllBytes(@"" + comboBox2.Text + "/jkpatch.elf");
                SendPayload(textBox1.Text, jkpatch, 9023);
                label1.Text = "JKPatch Loaded!";
            }
            catch(Exception) {
                label1.Text = "Error";
                MessageBox.Show("Unable to Send Payloads");
            }
        }

        private void button1_Click(object sender, EventArgs e) {
            label1.Text = "Connecting...";
            PS4 = new PS4RPC(textBox1.Text);

            try {
                PS4.Connect();
                label1.Text = "PS4 Connected";
                selectedProcess = null;
            }
            catch (Exception) {
                label1.Text = "Error";
                MessageBox.Show("Unable to connect to PS4");
            }
        }

        private void button3_Click(object sender, EventArgs e) {
            try {
                comboBox1.Items.Clear();

                pList = PS4.GetProcessList();
                foreach(Process p in pList.processes) {
                    comboBox1.Items.Add(p.name);
                }
            }
            catch (Exception) {
                label1.Text = "Error";
                MessageBox.Show("Unable to Grab Processes", "Are you connected?");
            }
        }

        private void button4_Click(object sender, EventArgs e) {
            if (pList != null) {
                try {
                    selectedProcess = comboBox1.SelectedItem.ToString();
                    label1.Text = "Process Selected";
                }
                catch(Exception) {
                    MessageBox.Show("You cannot select an empty process!");
                }
            }
            else {
                MessageBox.Show("Update Processes First");
            }
        }

        private void button5_Click(object sender, EventArgs e) {
            try {
                OpenFileDialog openFileDialog1 = new OpenFileDialog();
                openFileDialog1.Filter = "ELF Files|*.elf";
                openFileDialog1.Title = "Select a  File";

                if (openFileDialog1.ShowDialog() == System.Windows.Forms.DialogResult.OK) {
                    textBox2.Text = openFileDialog1.FileName;
                }
            }
            catch (Exception) {
                label1.Text = "Error";
                MessageBox.Show("Unable to open File Dialog");
            }
        }

        private void button6_Click(object sender, EventArgs e) {
            if(textBox2.Text != null && selectedProcess != null && pList != null) {
                try {
                    byte[] bytes = File.ReadAllBytes(textBox2.Text);
                    Process process = pList.FindProcess(selectedProcess);
                    PS4.LoadElf(process.pid, bytes);
                    label1.Text = "Elf Loaded";
                }
                catch (Exception) {
                    label1.Text = "Error";
                    MessageBox.Show("Unable to Load ELF");
                }
            }
            else {
                MessageBox.Show("Select a Process First");
            }
        }

        private void button7_Click(object sender, EventArgs e) {
            if (selectedProcess != null && pList != null) {
                try {
                    Process process = pList.FindProcess(selectedProcess);
                    MemoryData = PS4.ReadMemory(process.pid, Convert.ToUInt64(textBox3.Text, 16), Convert.ToInt32(textBox4.Text, 16));
                    MemoryStream stream = new MemoryStream(MemoryData);
                    DynamicFileByteProvider byteProvider = new DynamicFileByteProvider(stream);
                    hexBox1.ByteProvider = byteProvider;
                }
                catch (Exception) {
                    label1.Text = "Error";
                    MessageBox.Show("Unable to Peek Memory");
                }
            }
            else {
                MessageBox.Show("Select a Process First");
            }
        }

        private void button8_Click(object sender, EventArgs e) {
            if (selectedProcess != null && pList != null) {
                try {
                    DynamicFileByteProvider dynamicFileByteProvider = hexBox1.ByteProvider as DynamicFileByteProvider;
                    dynamicFileByteProvider.ApplyChanges();

                    Process process = pList.FindProcess(selectedProcess);
                    PS4.WriteMemory(process.pid, Convert.ToUInt64(textBox3.Text, 16), MemoryData);
                }
                catch (Exception) {
                    label1.Text = "Error";
                    MessageBox.Show("Unable to Poke Memory");
                }
            }
            else {
                MessageBox.Show("Select a Process First");
            }
        }

        private void button9_Click(object sender, EventArgs e) {
            var progress = new Progress<string>(s => richTextBox1.AppendText(s));
            listenerOn = true;

            richTextBox1.AppendText("Starting Listener\n");
            socketListener = Task.Factory.StartNew(() => SocketThread(progress), TaskCreationOptions.LongRunning);
        }

        private void button10_Click(object sender, EventArgs e) {
            listenerOn = false;

            IPAddress ipAddress = IPAddress.Parse("127.0.0.1");
            IPEndPoint localEndPoint = new IPEndPoint(ipAddress, 29386);
            Socket signalEnd = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            signalEnd.Connect(localEndPoint);
            signalEnd.Send(System.Text.Encoding.ASCII.GetBytes("Stopping Listener\n"));
            signalEnd.Shutdown(SocketShutdown.Both);
            signalEnd.Close();
        }

        private void button11_Click(object sender, EventArgs e) {
            richTextBox1.Clear();
        }


        private void SocketThread(IProgress<string> progress) {
            IPAddress ipAddress = IPAddress.Parse("127.0.0.1");
            IPEndPoint localEndPoint = new IPEndPoint(ipAddress, 29386);
            ServerListener = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

            try {
                ServerListener.Bind(localEndPoint);
                ServerListener.Listen(100);
            }
            catch(Exception) {
                listenerOn = false;
                progress.Report("Unable to Start Listener\n");
            }

            while(listenerOn) {
                try {
                    Socket client = ServerListener.Accept();

                    byte[] buffer = new byte[0x100];
                    client.Receive(buffer, 0x100, SocketFlags.None);

                    string output = System.Text.Encoding.ASCII.GetString(buffer);
                    progress.Report(output);

                    client.Shutdown(SocketShutdown.Both);
                    client.Close();
                }
                catch(Exception) {
                    listenerOn = false;
                    ServerListener.Close();
                }
            }

            ServerListener.Close();
        }

        private uint min(uint a, uint b) {
            if (a < b)
                return a;
            return b;
        }

        private void button12_Click(object sender, EventArgs e) {
            if (selectedProcess != null && pList != null) {
                try {
                    Process process = pList.FindProcess(selectedProcess);
                    uint address = Convert.ToUInt32(textBox5.Text, 16);
                    uint totalSize = Convert.ToUInt32(textBox6.Text, 16);
                    int fileOffset = 0;

                    FileStream stream = new FileStream(@"memoryDump.bin", FileMode.Create);

                    while(totalSize > 0) {
                        int sizeToDump = (int)min(0x10000, totalSize);
                        byte[] memory = PS4.ReadMemory(process.pid, address, sizeToDump);
                        stream.Write(memory, fileOffset, sizeToDump);

                        address += (uint)sizeToDump;
                        totalSize -= (uint)sizeToDump;
                        fileOffset += sizeToDump;
                    }

                    stream.Close();
                    MessageBox.Show("Memory dump complete!");
                }
                catch (Exception) {
                    label1.Text = "Error";
                    MessageBox.Show("Unable to Peek Memory");
                }
            }
            else {
                MessageBox.Show("Select a Process First");
            }
        }
    }
}
