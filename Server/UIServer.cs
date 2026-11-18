using System;
using System.Net;
using System.Threading;
using System.Net.Sockets;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace Server
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
                s.Flush();
            }
        }

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

    /// <summary>一条客户端连接的封装：Socket + NetworkStream + 发送锁</summary>
    internal class ClientConn
    {
        public Socket Socket;
        public NetworkStream Stream;
        public readonly object SendLock = new object();
        public string UserName;
    }

    public class server
    {
        public event EventHandler Started;
        public event EventHandler<UserLoggedEventArgs> Logged;
        public event EventHandler<PublicChatEventArgs> PChated;
        public event EventHandler<UserLoggedEventArgs> Logouted;
        public event EventHandler GetFile;

        public server() { }

        public IPAddress ip { get; set; }
        public int port { get; set; }

        Thread thread = null;
        private TcpListener listener;
        private string spath = "";

        private readonly object userlistLock = new object();
        private Dictionary<string, ClientConn> userlist = new Dictionary<string, ClientConn>();

        //--------------------------------------------------------------
        // 启动
        //--------------------------------------------------------------
        public void Start()
        {
            this.listener = new TcpListener(this.ip, this.port);
            this.listener.Start();
            this.thread = new Thread(this.Work) { IsBackground = true };
            this.thread.Start();
            if (this.Started != null) this.Started(this, EventArgs.Empty);
        }

        public void Work()
        {
            try
            {
                while (true)
                {
                    Socket s = this.listener.AcceptSocket();
                    var t = new Thread(() => this.ResponseClient(s)) { IsBackground = true };
                    t.Start();
                }
            }
            catch (SocketException) { }
            catch (ObjectDisposedException) { }
        }

        public void StopClient()
        {
            try { this.listener.Stop(); } catch { }
            lock (userlistLock)
            {
                foreach (var c in userlist.Values) { try { c.Socket.Close(); } catch { } }
                userlist.Clear();
            }
        }

        //--------------------------------------------------------------
        // 单连接主循环
        //--------------------------------------------------------------
        private void ResponseClient(Socket soket)
        {
            NetworkStream ns = null;
            ClientConn conn = null;

            try
            {
                ns = new NetworkStream(soket, false);
                conn = new ClientConn { Socket = soket, Stream = ns };

                while (true)
                {
                    string message = FrameProtocol.ReadMessage(ns);
                    if (message == null) break;                 // 对端关闭
                    string[] tokens = message.Split('|');
                    string cmd = tokens[0];

                    if (cmd == "CONN")
                    {
                        conn.UserName = tokens[1];
                        bool dup = false;
                        lock (userlistLock)
                        {
                            if (userlist.ContainsKey(tokens[1])) dup = true;
                            else userlist.Add(tokens[1], conn);
                        }
                        if (dup) break;

                        var e1 = new UserLoggedEventArgs();
                        e1.UserName = tokens[1];
                        e1.UserList = this.GetUserList();
                        if (this.Logged != null) this.Logged(this, e1);
                    }
                    else if (cmd == "OUT")
                    {
                        RemoveUser(conn);
                        break;
                    }
                    else if (cmd == "TS")
                    {
                        var e1 = new PublicChatEventArgs();
                        e1.UserName   = tokens[1];
                        e1.ToUserName = tokens[2];
                        e1.Time       = tokens[3];
                        e1.Msg        = string.Join("|", tokens, 4, tokens.Length - 4);
                        if (this.PChated != null) this.PChated(this, e1);
                    }
                    else if (cmd == "SFS")
                    {
                        this.HandleFileSend(conn, tokens);
                    }
                    else if (cmd == "CFC")
                    {
                        this.SendFileTo(conn);
                    }
                }
            }
            catch (IOException) { }
            catch (ObjectDisposedException) { }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("ResponseClient: " + ex);
            }
            finally
            {
                RemoveUser(conn);
                if (ns != null) { try { ns.Close(); } catch { } }
                if (soket != null) { try { soket.Close(); } catch { } }
            }
        }

        private void RemoveUser(ClientConn conn)
        {
            if (conn == null || conn.UserName == null) return;
            bool removed;
            lock (userlistLock) removed = userlist.Remove(conn.UserName);
            if (removed)
            {
                string name = conn.UserName;
                conn.UserName = null;
                var e1 = new UserLoggedEventArgs();
                e1.UserName = name;
                e1.UserList = this.GetUserList();
                if (this.Logouted != null) this.Logouted(this, e1);
            }
        }

        //--------------------------------------------------------------
        // 文件转发 / 落盘
        //--------------------------------------------------------------
        private void HandleFileSend(ClientConn conn, string[] tokens)
        {
            if (tokens.Length < 5) return;

            string filename = tokens[1];
            long   length;
            if (!long.TryParse(tokens[2], out length)) return;
            string from = tokens[3];
            string to   = tokens[4];

            if (to != "Server")
            {
                ClientConn toConn;
                lock (userlistLock) userlist.TryGetValue(to, out toConn);
                if (toConn == null) return;                  // 目标不在线，静默放弃

                FrameProtocol.SendMessage(conn.Stream, "CFS|OK", conn.SendLock);

                long receive = 0;
                byte[] buf = new byte[8192];

                // 关键：SFC 头帧 + 文件字节必须原子发送给 toConn，
                // 否则中间插入的 CHAT 帧会被接收端当作文件内容吃掉。
                lock (toConn.SendLock)
                {
                    FrameProtocol.SendMessage(
                        toConn.Stream,
                        "SFC|" + filename + "|" + length + "|" + from,
                        toConn.SendLock);                    // Monitor 可重入

                    while (receive < length)
                    {
                        int want = (int)Math.Min(buf.Length, length - receive);
                        int n = conn.Stream.Read(buf, 0, want);
                        if (n <= 0) throw new IOException("文件传输中断");
                        toConn.Stream.Write(buf, 0, n);      // 按实际字节数写
                        receive += n;
                    }
                }
            }
            else
            {
                FrameProtocol.SendMessage(conn.Stream, "CFS|OK", conn.SendLock);

                long receive = 0;
                byte[] buf = new byte[8192];
                using (var writer = new FileStream(Path.Combine("./UpLoad/", filename),
                                                   FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    while (receive < length)
                    {
                        int want = (int)Math.Min(buf.Length, length - receive);
                        int n = conn.Stream.Read(buf, 0, want);
                        if (n <= 0) throw new IOException("文件传输中断");
                        writer.Write(buf, 0, n);
                        receive += n;
                    }
                }
            }

            if (this.GetFile != null) this.GetFile(this, EventArgs.Empty);
        }

        //--------------------------------------------------------------
        // 对外发送 API
        //--------------------------------------------------------------
        private List<string> GetUserList()
        {
            List<string> list = new List<string>();
            lock (userlistLock)
                foreach (var kv in userlist) list.Add(kv.Key);
            return list;
        }

        public void SendMessage(string touser, string message)
        {
            ClientConn c;
            lock (userlistLock) userlist.TryGetValue(touser, out c);
            if (c == null) return;
            FrameProtocol.SendMessage(c.Stream, message, c.SendLock);
        }

        /// <summary>广播：跳过 tokens[1] 指定的用户</summary>
        public void SendMessage(string message)
        {
            string[] tokens = message.Split('|');
            List<ClientConn> targets = new List<ClientConn>();
            lock (userlistLock)
            {
                foreach (var kv in userlist)
                    if (tokens.Length < 2 || tokens[1] != kv.Key)
                        targets.Add(kv.Value);
            }
            foreach (var c in targets)
            {
                try { FrameProtocol.SendMessage(c.Stream, message, c.SendLock); }
                catch { /* 忽略单连接写失败 */ }
            }
        }

        /// <summary>为兼容旧调用保留；新流程在 HandleFileSend 内已完成头部发送</summary>
        public void SendFileName(string tpath, string[] tokens)
        {
            this.spath = tpath;
            if (tokens.Length < 5) return;
            string sendStr = "SFC|" + Path.GetFileName(tpath) + "|" + tokens[2] + "|" + tokens[3];
            this.SendMessage(tokens[4], sendStr);
        }

        /// <summary>服务器主动把 spath 文件发给某连接（对应 CFC）</summary>
        public void SendFile(Socket soket)
        {
            if (string.IsNullOrEmpty(this.spath) || !File.Exists(this.spath)) return;

            using (var reader = new FileStream(this.spath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                byte[] buf = new byte[8192];
                int n;
                while ((n = reader.Read(buf, 0, buf.Length)) > 0)
                    soket.Send(buf, 0, n, SocketFlags.None);   // 按实际字节数发送
            }
        }

        private void SendFileTo(ClientConn conn)
        {
            if (string.IsNullOrEmpty(this.spath) || !File.Exists(this.spath)) return;

            lock (conn.SendLock)
            {
                using (var reader = new FileStream(this.spath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    byte[] buf = new byte[8192];
                    int n;
                    while ((n = reader.Read(buf, 0, buf.Length)) > 0)
                        conn.Stream.Write(buf, 0, n);
                }
            }
        }
    }

    //------------------------------------------------------------------
    // 事件参数（保持原样）
    //------------------------------------------------------------------
    public class UserLoggedEventArgs : EventArgs
    {
        public string UserName { get; set; }
        public List<string> UserList { get; set; }
    }

    public class PublicChatEventArgs : EventArgs
    {
        public string UserName { get; set; }
        public string ToUserName { get; set; }
        public string Msg { get; set; }
        public string Time { get; set; }
        public string Font { get; set; }
        public string FontColor { get; set; }
        public string FontSize { get; set; }
    }
}