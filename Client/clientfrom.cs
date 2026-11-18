using System;
using System.Net;
using System.Drawing;
using System.Windows.Forms;
using System.Net.Sockets;
using System.IO;
using System.Text;
using Client;
using IniFile;

public class ClientForm : Form
{
    private RichTextBox rttxt;
    private RichTextBox msgtxt;
    private ListBox userlist;
    private Button btnUseVehicles;
    private Button btncancel;
    private System.ComponentModel.IContainer components;
    private ContextMenuStrip onlymenu;
    private NotifyIcon notifyIcon1;
    private GroupBox groupBox1;
    private GroupBox groupBox2;
    private ProgressBar filestatus;
    private Label filelabel;
    private string spath = "";

    public ClientForm()
    {
        this.rttxt = new RichTextBox();
        this.msgtxt = new RichTextBox();
        this.userlist = new ListBox();
        this.btnUseVehicles = new Button();
        this.btncancel = new Button();
        this.components = new System.ComponentModel.Container();
        this.notifyIcon1 = new NotifyIcon(this.components);
        this.notifyIcon1.Icon = new Icon("12.ico");
        this.notifyIcon1.MouseDoubleClick += new MouseEventHandler(this.onlymenu_Click);
        this.onlymenu = new ContextMenuStrip();
        this.onlymenu.Items.Add("show");
        this.onlymenu.Items[0].Click += new EventHandler(this.onlymenu0_Click);
        this.onlymenu.Items.Add("eixt");
        this.onlymenu.Items[1].Click += new EventHandler(this.onlymenu1_Click);
        this.notifyIcon1.ContextMenuStrip = this.onlymenu;
        this.groupBox1 = new GroupBox();
        this.groupBox2 = new GroupBox();
        this.filestatus = new ProgressBar();
        this.filelabel = new Label();
        this.SuspendLayout();

        this.AutoScaleBaseSize = new System.Drawing.Size(5, 13);
        this.ClientSize = new System.Drawing.Size(590, 380);

        this.rttxt.Width = this.Width - 190;
        this.rttxt.Height = this.rttxt.Height = this.Height - 220;
        this.rttxt.Location = new System.Drawing.Point(150, 25);
        this.rttxt.Font = new Font("Meiryo UI",12,this.rttxt.Font.Style);
        this.rttxt.ReadOnly = true;

        this.msgtxt.Width = this.Width - 295;
        this.msgtxt.Height = 100;
        this.msgtxt.Location = new System.Drawing.Point(140, this.rttxt.Height + 70);
        this.msgtxt.Font = new Font("Meiryo UI",12,this.msgtxt.Font.Style);
        this.msgtxt.DragOver += new DragEventHandler(this.msgtxt_DragEnter);
        this.msgtxt.DragDrop += new DragEventHandler(this.msgtxt_DragDrop);

        this.userlist.MultiColumn = true;
        this.userlist.SelectionMode = SelectionMode.One;
        this.userlist.Width = 100;
        this.userlist.Height = 345;
        this.userlist.Location = new System.Drawing.Point(20, 25);
        this.userlist.Font = new Font("Meiryo UI",12,this.userlist.Font.Style);

        this.btnUseVehicles.Text = "Send Message";
        this.btnUseVehicles.Width = 100;
        this.btnUseVehicles.Height = 50;
        this.btnUseVehicles.Location = new System.Drawing.Point(167 + this.msgtxt.Width, this.rttxt.Height + 70);
        this.btnUseVehicles.Click += new EventHandler(this.btnStart_Click);

        this.groupBox1.Text = "User List";
        this.groupBox1.Controls.Add(this.userlist);
        this.groupBox1.Width = 120;
        this.groupBox1.Height = 365;
        this.groupBox1.Location = new System.Drawing.Point(10, 5);

        this.groupBox2.Text = "Messages";
        this.groupBox2.Controls.Add(this.rttxt);
        this.groupBox2.Width = this.rttxt.Width + 20;
        this.groupBox2.Height = this.rttxt.Height + 30;
        this.groupBox2.Location = new System.Drawing.Point(140, 5);

        this.filestatus.Text = "bar";
        this.filestatus.Width = 200;
        this.filestatus.Height = 30;
        this.filestatus.Location = new System.Drawing.Point(140, this.rttxt.Height + 36);

        this.filelabel.BackColor = Color.Transparent;
        this.filelabel.Parent = this.filestatus;
        this.filelabel.BorderStyle = BorderStyle.FixedSingle;
        this.filelabel.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
        this.filelabel.Width = this.filestatus.Width;
        this.filelabel.Height = this.filestatus.Height;
        this.filelabel.Location = new System.Drawing.Point(140, this.rttxt.Height + 36);

        this.btncancel.Text = "X";
        this.btncancel.Width = 30;
        this.btncancel.Height = 30;
        this.btncancel.Location = new System.Drawing.Point(340, this.rttxt.Height + 36);
        this.btncancel.Click += new EventHandler(this.btncancel_Click);

        this.Controls.Add(this.rttxt);
        this.Controls.Add(this.msgtxt);
        this.Controls.Add(this.userlist);
        this.Controls.Add(this.btnUseVehicles);
        this.Controls.Add(this.groupBox1);
        this.Controls.Add(this.groupBox2);
        this.Name = "Form1";
        this.Text = "Calculator Box";
        this.StartPosition = FormStartPosition.CenterScreen;
        this.AutoScroll = true;
        this.ResumeLayout(false);
        Form.CheckForIllegalCrossThreadCalls = false;
        this.Load += new EventHandler(Form1_Load);
        this.FormClosing += new FormClosingEventHandler(this.Form1_FormClosing);
        this.SizeChanged += new EventHandler(this.Form1_SizeChanged);
    }

    Client.client s = new Client.client();
    IPAddress ip = null;
    int port = 0;
    string clientName = "";
    private void Form1_Load(object sender, EventArgs e)
    {
        readini();
        this.msgtxt.AllowDrop = true;
        this.AllowDrop = true;
        this.Text = "ClientUser:" + this.clientName;
        this.s.Started += new EventHandler(s_Started);
        this.s.Logged += new EventHandler<LoggedEventArgs>(s_Logged);
        this.s.PChated += new EventHandler<PublicChatEventArgs>(s_PChated);
        this.s.StatusBar += new EventHandler<StatusBarEventArgs>(ChgProgress);
        this.s.GetFileStart += new EventHandler<FileEventArgs>(s_GetFileStart);
        this.s.GetFileEnd += new EventHandler<FileEventArgs>(s_GetFileEnd);
        this.s.SendFileEnd += new EventHandler(s_SendFileEnd);
        this.s.ip = this.ip;
        this.s.port = this.port;
        this.s.clientName = this.clientName;
        try
        {
            this.s.Connect();
        }
        catch (System.Net.Sockets.SocketException ex)
        {
            MessageBox.Show("服务器未启动,点击OK关闭程序:\n" + ex.Message);
            this.s.StopClient();
            this.Dispose();
            this.Close();
            // 这里别 Application.Exit,让用户改端口或重开
        }
    }

    private void Form1_FormClosing(object sender, FormClosingEventArgs e)
    {
        if (MessageBox.Show("Exit the window?", "Exit",
                        MessageBoxButtons.OKCancel,
                        MessageBoxIcon.Warning) != DialogResult.OK)
        {
            e.Cancel = true;
            return;
        }
        this.s.SendMessage("OUT|" + this.clientName);
        this.s.StopClient();
        // 不 Cancel,让窗体自然关闭
    }

    private void Form1_SizeChanged(object sender, EventArgs e)
    {
        if (this.WindowState == FormWindowState.Minimized)
        {
            this.Hide();
            this.notifyIcon1.Visible=true;
        }else {
            this.groupBox1.Height = this.Height - 55;
            this.userlist.Height = this.groupBox1.Height - 25;
            this.rttxt.Height = this.Height - 220;
            this.groupBox2.Height = this.rttxt.Height + 30;
            this.msgtxt.Location = new System.Drawing.Point(140, this.rttxt.Height + 70);
            this.rttxt.Width = this.Width - 190;
            this.groupBox2.Width = this.rttxt.Width + 20;
            this.msgtxt.Width = this.Width - 295;
            this.btnUseVehicles.Location = new System.Drawing.Point(167 + this.msgtxt.Width, this.rttxt.Height + 70);
            this.filestatus.Location = new System.Drawing.Point(140, this.rttxt.Height + 36);
            this.filelabel.Location = new System.Drawing.Point(140, this.rttxt.Height + 36);
            this.btncancel.Location = new System.Drawing.Point(340, this.rttxt.Height + 36);
        }
    }

    private void onlymenu_Click(object sender, MouseEventArgs e)
    {
        if (this.WindowState == FormWindowState.Minimized)
        {
            this.Visible = true;
            this.WindowState = FormWindowState.Normal;
            this.notifyIcon1.Visible = false;
        }
    }

    private void onlymenu0_Click(object sender, EventArgs e)
    {
        this.Visible = true;
        this.WindowState = FormWindowState.Normal;
        this.notifyIcon1.Visible = false;
    }

    private void onlymenu1_Click(object sender, EventArgs e)
    {
        this.notifyIcon1.Visible = false;
        this.Close();   // 走统一入口,别在外部再调 ExitMsg
    }

    private void btnStart_Click(object sender, EventArgs e)
    {
        if (this.userlist.SelectedItem == null)
        {
            MessageBox.Show("请先在左侧列表选一个用户");
            return;
        }
        if (this.spath != "") {
            this.rttxt.AppendText(Convert.ToDateTime(DateTime.Now.ToLongTimeString()) + " " + this.userlist.SelectedItem + " ユーザーへファイルの送信を開始します。 \n");
            this.s.SendFileName(this.spath, this.userlist.SelectedItem.ToString());
            this.msgtxt.Focus();
            this.spath = "";
        } else {
            this.rttxt.AppendText(this.clientName +" in " + Convert.ToDateTime(DateTime.Now.ToLongTimeString()) + " sayTo " + this.userlist.SelectedItem.ToString() + "： \n" + this.msgtxt.Text + "\n");
            this.s.SendMessage("TS|" + this.clientName + "|" + this.userlist.SelectedItem.ToString() + "|" + DateTime.Now.ToLongTimeString() + "|" + this.msgtxt.Text);
            this.msgtxt.Text = "";
            this.msgtxt.Focus();
        }
    }

    private void s_Started(object sender, EventArgs e)
    {
    }

    private void s_Logged(object sender, LoggedEventArgs e)
    {
        this.rttxt.AppendText(e.Msg);
        this.userlist.Items.Clear();
        foreach (string item in e.UserList)
        {
            if (item != this.clientName)
            {
                this.userlist.Items.Add(item);
            }
        }
        if (this.userlist.Items.Count > 0) this.userlist.SetSelected(0, true);
    }

    private void msgtxt_DragEnter(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effect = DragDropEffects.Link;
            this.msgtxt.Cursor = System.Windows.Forms.Cursors.Arrow;
        }
        else
        {
            e.Effect = DragDropEffects.None;
        }
    }

    private void msgtxt_DragDrop(object sender, DragEventArgs e)
    {
        this.spath = ((System.Array)e.Data.GetData(DataFormats.FileDrop)).GetValue(0).ToString();
        this.filelabel.Text = Path.GetFileName(this.spath);
        this.Controls.Add(this.filelabel);
        this.Controls.Add(this.filestatus);
        this.Controls.Add(this.btncancel);
        this.msgtxt.Cursor = System.Windows.Forms.Cursors.IBeam;
    }

    private void s_PChated(object sender, PublicChatEventArgs e)
    {
        this.rttxt.AppendText(e.UserName + " in " + e.Time + " sayTo " + e.ToUserName + "： \n" + e.Msg + "\n");
    }

    private void ChgProgress(object sender, StatusBarEventArgs e)
    {
        this.Controls.Remove(this.filelabel);
        this.filestatus.Maximum = (int)e.positionLength;
        this.filestatus.Minimum = 0;
        this.filestatus.Value = (int)e.lngPosition;
    }

    private void btncancel_Click(object sender, EventArgs e)
    {
        this.SendEndCancel();
    }

    private void SendEndCancel()
    {
        this.Controls.Remove(this.filestatus);
        this.Controls.Remove(this.btncancel);
        this.spath = "";
    }

    private void s_GetFileStart(object sender, FileEventArgs e)
    {
        this.rttxt.AppendText(Convert.ToDateTime(DateTime.Now.ToLongTimeString()) + " " + e.UserName + " から " + e.filename + "ファイルの受信を開始します！ \n");
    }

    private void s_GetFileEnd(object sender, FileEventArgs e)
    {
        this.rttxt.AppendText(Convert.ToDateTime(DateTime.Now.ToLongTimeString()) + " " + e.UserName + " から " + e.filename + "ファイルの受信を完了しました！ \n");
    }

    private void s_SendFileEnd(object sender, EventArgs e)
    {
        this.rttxt.AppendText(Convert.ToDateTime(DateTime.Now.ToLongTimeString()) + " ファイルの送信を完了しました！ \n");
    }

    private void readini()
    {
        string section= "config";
        IniFile.editini ini = new IniFile.editini("./config.ini");
        if (ini.ExistINIFile())
        {
            this.ip = IPAddress.Parse(ini.IniReadValue( section, "host"));
            this.port = int.Parse(ini.IniReadValue( section, "port"));
            this.clientName = ini.IniReadValue( section, "name");
        }
        else
        {
            this.ip = IPAddress.Parse("9.123.200.8");
            this.port = int.Parse("3000");
            this.clientName = "xuwei";
            MessageBox.Show("config.ini is not found! \nDeflut value set!");
        }
    }

    private void ExitMsg()
    {
        if (MessageBox.Show("Exit the window?", "Exit", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) == DialogResult.OK)
        {
            this.s.SendMessage("OUT|" + this.clientName);
            this.s.StopClient();
            this.Dispose();
//            Application.Exit();
            this.Close();
        }
        else
        {
        }
    }
    [STAThreadAttribute]
    private static void Main()
    {
        Application.Run(new ClientForm());
    }
}