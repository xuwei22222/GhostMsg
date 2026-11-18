using System;
using System.Net;
using System.Threading;
using System.Net.Sockets;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace Client
{
    /// <summary>帧协议：4 字节小端长度前缀 + Unicode 消息体</summary>
    internal static class FrameProtocol
    {
        public static void SendMessage(Stream s, string message, object sync)
        {
            byte[] body = Encoding.Unicode.GetBytes(message);
            byte[] len  = BitConverter.GetBytes(body.Length);
            lock (sync)
            {
                s.Write(len, 0, 4);
                s.Write(body, 0, body.Length);
                s.Flush();   // NetworkStream 上是 no-op，保留无害
            }
        }

        /// <summary>读一条完整消息；连接关闭返回 null</summary>
        public static string ReadMessage(Stream s)
        {
            byte[] lenBuf = new byte[4];
            if (!ReadExact(s, lenBuf, 4)) return null;
            int n = BitConverter.ToInt32(lenBuf, 0);
            if (n <= 0 || n > 64 * 1024 * 1024)
                throw new IOException("非法帧长度: " + n);
            byte[] body = new byte[n];
            if (!ReadExact(s, body, n)) return null;
            return Encoding.Unicode.GetString(body);
        }

        public static bool ReadExact(Stream s, byte[] buf, int count)
        {
            int off = 0;
            while (off < count)
            {
                int r = s.Read(buf, off, count - off);
                if (r <= 0) return false;
                off += r;
            }
            return true;
        }
    }

    public class client
    {
        public client() { }

        public event EventHandler Started;
        public event EventHandler<LoggedEventArgs> Logged;
        public event EventHandler<PublicChatEventArgs> PChated;
        public event EventHandler<StatusBarEventArgs> StatusBar;
        public event EventHandler<FileEventArgs> GetFileStart;
        public event EventHandler<FileEventArgs> GetFileEnd;
        public event EventHandler SendFileEnd;

        public string clientName { get; set; }
        public string msg { get; set; }
        public IPAddress ip { get; set; }
        public int port { get; set; }

        TcpClient tclient = new TcpClient();
        NetworkStream stream;
        Thread mainThread = null;
        public string spath = "";

        // 所有对 stream 的写入都必须持有此锁，确保"消息帧"与"文件字节流"不会互相穿插
        readonly object sendLock = new object();

        //--------------------------------------------------------------
        // 连接
        //--------------------------------------------------------------
        public void Connect()
        {
            this.tclient.Connect(this.ip, this.port);
            this.stream = this.tclient.GetStream();
            this.SendMessage("CONN|" + this.clientName);

            this.mainThread = new Thread(this.ResponseServer);
            this.mainThread.IsBackground = true;
            this.mainThread.SetApartmentState(ApartmentState.STA);
            this.mainThread.Start();

            if (this.Started != null) this.Started(this, EventArgs.Empty);
        }

        public void StopClient()
        {
            try { this.tclient.Close(); } catch { }
        }

        //--------------------------------------------------------------
        // 接收主循环
        //--------------------------------------------------------------
        public void ResponseServer()
        {
            try
            {
                while (true)
                {
                    string message = FrameProtocol.ReadMessage(this.stream);
                    if (message == null) break;                // 对端关闭
                    string[] tokens = message.Split('|');

                    if (tokens[0] == "CHAT")
                    {
                        var e1 = new PublicChatEventArgs();
                        e1.UserName   = tokens[1];
                        e1.ToUserName = tokens[2];
                        e1.Time       = Convert.ToDateTime(tokens[3]);
                        e1.Msg        = string.Join("|", tokens, 4, tokens.Length - 4);
                        if (this.PChated != null) this.PChated(this, e1);
                    }
                    else if (tokens[0] == "CU")
                    {
                        var e1 = new LoggedEventArgs();
                        e1.UserList = new List<string>(tokens[1].Split(','));
                        e1.UserName = tokens[2];
                        e1.Msg      = string.Join("|", tokens, 3, tokens.Length - 3);
                        if (this.Logged != null) this.Logged(this, e1);
                    }
                    else if (tokens[0] == "CFS")
                    {
                        // 服务器已准备好接收，开始发送本地文件字节流
                        this.SendFile();
                    }
                    else if (tokens[0] == "SFC")
                    {
                        this.ReceiveFile(tokens);
                    }
                }
            }
            catch (IOException) { }
            catch (ObjectDisposedException) { }
            catch (Exception ex)
            {
                // 建议记录日志
                System.Diagnostics.Debug.WriteLine("ResponseServer: " + ex);
            }
        }

        //--------------------------------------------------------------
        // 接收文件：严格读 length 字节
        //--------------------------------------------------------------
        private void ReceiveFile(string[] tokens)
        {
            string filename = tokens[1];
            long   length   = Convert.ToInt64(tokens[2]);
            var e1 = new FileEventArgs();
            e1.UserName = tokens[3];
            e1.filename = filename;
            if (this.GetFileStart != null) this.GetFileStart(this, e1);

            long receive = 0;
            byte[] buf = new byte[8192];

            string savePath = Path.Combine("./", filename);
            using (var writer = new FileStream(savePath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                while (receive < length)
                {
                    int want = (int)Math.Min(buf.Length, length - receive);
                    // NetworkStream 不会预读，只会返回 ≤ want 的字节，所以后续帧不会丢
                    int n = this.stream.Read(buf, 0, want);
                    if (n <= 0) throw new IOException("文件传输中断");
                    writer.Write(buf, 0, n);
                    receive += n;
                }
            }

            if (this.GetFileEnd != null) this.GetFileEnd(this, e1);
        }

        //--------------------------------------------------------------
        // 发送消息
        //--------------------------------------------------------------
        public void SendMessage(string message)
        {
            FrameProtocol.SendMessage(this.stream, message, this.sendLock);
        }

        /// <summary>发送 SFS 请求，服务器回 CFS 后由 SendFile 实际发送</summary>
        public void SendFileName(string tpath, string toname)
        {
            this.spath = tpath;
            long length = new FileInfo(tpath).Length;
            this.SendMessage("SFS|" + Path.GetFileName(tpath) + "|" + length + "|" + this.clientName + "|" + toname);
        }

        /// <summary>发送文件字节流。整个传输期间独占 sendLock，防止消息帧插入。</summary>
        public void SendFile()
        {
            long total, sent = 0;
            byte[] buf = new byte[8192];

            lock (this.sendLock)
            {
                using (var reader = new FileStream(this.spath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    total = reader.Length;
                    int n;
                    while ((n = reader.Read(buf, 0, buf.Length)) > 0)
                    {
                        this.stream.Write(buf, 0, n);
                        sent += n;
                        // 注意：handler 请用 BeginInvoke 异步刷 UI，避免与 UI 线程上的 SendMessage 互锁
                        if (this.StatusBar != null)
                        {
                            var sb = new StatusBarEventArgs();
                            sb.lngPosition    = sent;
                            sb.positionLength = total;
                            this.StatusBar(this, sb);
                        }
                    }
                }
            }
            if (this.SendFileEnd != null) this.SendFileEnd(this, EventArgs.Empty);
        }
    }

    //------------------------------------------------------------------
    // 事件参数（保持原样）
    //------------------------------------------------------------------
    public class StatusBarEventArgs : EventArgs
    {
        public long lngPosition { get; set; }
        public long positionLength { get; set; }
    }

    public class LoggedEventArgs : EventArgs
    {
        public string UserName { get; set; }
        public string Msg { get; set; }
        public List<string> UserList { get; set; }
    }

    public class PublicChatEventArgs : EventArgs
    {
        public string UserName { get; set; }
        public string ToUserName { get; set; }
        public string Msg { get; set; }
        public DateTime Time { get; set; }
        public string Font { get; set; }
        public string FontColor { get; set; }
        public string FontSize { get; set; }
    }

    public class FileEventArgs : EventArgs
    {
        public string UserName { get; set; }
        public string filename { get; set; }
    }
}