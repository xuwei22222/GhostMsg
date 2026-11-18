using System;
using System.Net;
using System.Drawing;
using System.Windows.Forms;
using System.Net.Sockets;
using Server;

public class MyForm : Form
{
    private RichTextBox rttxt;
    private Button btnUseVehicles;
    private System.ComponentModel.IContainer components;
    private ContextMenuStrip onlymenu;
    private NotifyIcon notifyIcon1;

    public MyForm()
    {
        this.rttxt = new RichTextBox();
        this.btnUseVehicles = new Button();
        this.components = new System.ComponentModel.Container();
        this.notifyIcon1 = new NotifyIcon(this.components);
        this.notifyIcon1.Icon = new Icon("12.ico");
        this.onlymenu = new ContextMenuStrip();
        this.onlymenu.Items.Add("show");
        this.onlymenu.Items[0].Click += new EventHandler(this.onlymenu0_Click);
        this.onlymenu.Items.Add("eixt");
        this.onlymenu.Items[1].Click += new EventHandler(this.onlymenu1_Click);
        this.notifyIcon1.ContextMenuStrip = this.onlymenu;
        this.SuspendLayout();

        this.rttxt.Width = 300;
        this.rttxt.Height = 100;
        this.rttxt.Location = new System.Drawing.Point(20, 20);

        this.btnUseVehicles.Text = "Click Me";
        this.btnUseVehicles.Width = 100;
        this.btnUseVehicles.Height = 50;
        this.btnUseVehicles.Location = new System.Drawing.Point(20, 200);
        this.btnUseVehicles.Click += new EventHandler(this.btnStart_Click);

        this.AutoScaleBaseSize = new System.Drawing.Size(5, 13);
        this.ClientSize = new System.Drawing.Size(416, 416);
        this.Controls.Add(this.rttxt);
        this.Controls.Add(this.btnUseVehicles);
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

    Server.server s = new Server.server();
    IPAddress ip = null;
    private void Form1_Load(object sender, EventArgs e)
    {
        string ip = string.Empty;
        foreach (IPAddress _IPAddress in Dns.GetHostEntry(Dns.GetHostName()).AddressList) {
            if (_IPAddress.AddressFamily == AddressFamily.InterNetwork) {
                this.ip = _IPAddress;
                this.Text = "ServerIP:" + this.ip.ToString();
//                this.rttxt.AppendText("IPAddress:" + this.ip.ToString() + "\n");
            }
        }
//        ip = Dns.Resolve(Dns.GetHostName()).AddressList[0];
        this.s.Started += new EventHandler(s_Started);
        this.s.Logged += new EventHandler<UserLoggedEventArgs>(s_Logged);
        this.s.PChated += new EventHandler<PublicChatEventArgs>(s_PChated);
        this.s.Logouted += new EventHandler<UserLoggedEventArgs>(s_Logouted);
        this.s.GetFile += new EventHandler(s_GetFile);
        this.s.ip = this.ip;
        this.s.port = int.Parse("3000");
        try
        {
            this.s.Start();
        }
        catch (System.Net.Sockets.SocketException ex)
        {
            MessageBox.Show("端口 3000 已被占用或无法绑定:\n" + ex.Message);
            // 这里别 Application.Exit,让用户改端口或重开
        }
    }

    private void Form1_FormClosing(object sender, FormClosingEventArgs e)
    {
        if (MessageBox.Show("Exit the window?", "Exit", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) == DialogResult.OK)
        {
            Environment.Exit(0);
            this.Dispose();
            Application.Exit();
        }
        else
        {
            e.Cancel = true;
        }
    }

    private void Form1_SizeChanged(object sender, EventArgs e)
    {
        if (WindowState == FormWindowState.Minimized)
        {
            this.Hide();
            this.notifyIcon1.Visible=true;
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
        if (MessageBox.Show("Exit the window?", "Exit", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) == DialogResult.OK)
        {
            this.s.StopClient();
            this.Dispose();
            Application.Exit();
        }
        else
        {
        }
    }

    private void btnStart_Click(object sender, EventArgs e)
    {
        
    }

    private void s_Started(object sender, EventArgs e)
    {
        this.rttxt.AppendText("****************** Server Started ******************\n");
    }

    private void s_Logged(object sender, UserLoggedEventArgs e)
    {
        this.rttxt.AppendText("client【 "+e.UserName + "】 Started！\n");
        this.s.SendMessage("CU|Server," + string.Join(",", e.UserList.ToArray()) + "|" + e.UserName + "|【" + e.UserName + "】 Started！\n");
    }

    private void s_Logouted(object sender, UserLoggedEventArgs e)
    {
        this.rttxt.AppendText("client【 " + e.UserName + "】 Logouted！\n");
        this.s.SendMessage("CU|Server," + string.Join(",", e.UserList.ToArray()) + "|" + e.UserName + "|【" + e.UserName + "】 Logouted！\n");
    }

    private void s_GetFile(object sender, EventArgs e)
    {
//        this.rttxt.AppendText(e.UserName + "\n");
//        this.s.SendMessage("CU|Server," + string.Join(",", e.UserList.ToArray()) + "|" + e.UserName + "|【" + e.UserName + "】 Logouted！\n");
    }

    private void s_PChated(object sender, PublicChatEventArgs e)
    {
        this.rttxt.AppendText(e.UserName + " in " + Convert.ToDateTime(e.Time) + " sayTo " + e.ToUserName + "： \n" + e.Msg + "\n");
        if (e.ToUserName == "Server")
        {
            this.s.SendMessage("CHAT|" + e.UserName + "|" + e.ToUserName + "|" + e.Time + "|" + e.Msg);
        } else
        {
            this.s.SendMessage(e.ToUserName, "CHAT|" + e.UserName + "|" + e.ToUserName + "|" + e.Time + "|" + e.Msg);
        }
    }

    private static void Main()
    {
        Application.Run(new MyForm());
    }
}