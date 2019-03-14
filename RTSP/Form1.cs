using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

using AxDXMediaPlayerLib;
using System.Runtime.InteropServices;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;

namespace RTSP
{
    public partial class Form1 : Form
    {
        int frame = 0;
        public Form1()
        {
            InitializeComponent();

            axDXMediaPlayer1.OnDXMediaPlayerEvent += dxPlayer_OnDXMediaPlayerEvent;
            axDXMediaPlayer1.OnAsyncResultEvent += dxPlayer_OnAsyncResultEvent;
            axDXMediaPlayer1.OnVideoDataEvent += dxPlayer_OnVideoDataEvent;

            StartCamera("rtsp://192.168.1.30:554/stream2");
        }

        private enum DXPLAYER_STATE { STATE_STOPPED, STATE_OPENED, STATE_PLAYING, STATE_PAUSED }

        // DXMediaPlayer 이벤트
        private const int PLAYER_EVENT_VIDEOSIZE = 0;
        private const int PLAYER_EVENT_STOPPED = 1;
        private const int PLAYER_EVENT_PLAYTIME = 2;
        private const int PLAYER_EVENT_STREAM_INFO = 5;
        private const int PLAYER_EVENT_FULLSCREEN = 7;
        private const int PLAYER_EVENT_FILE_READ_STOPPED = 8;

        // 비동기 함수 결과 이벤트
        private const int ASYNC_RESULT_CONNECTPLAY = 0;

        private string rtspURL;

        private Thread threadCameraConnect;
        private bool bThreadCameraConnect = false;
        private const int RECONNECT_INTERVAL = 10;
        private bool bConnecting = false;

        public void StartCamera(string url)
        {
            if (!bThreadCameraConnect)
            {
                rtspURL = url;
                bThreadCameraConnect = true;
                threadCameraConnect = new Thread(new ThreadStart(ThreadCameraConnect));
                threadCameraConnect.IsBackground = true;
                threadCameraConnect.Start();
            }
        }

        public void StopCamera()
        {
            if (bThreadCameraConnect)
            {
                bThreadCameraConnect = false;
                threadCameraConnect.Join();
            }
        }

        private void ThreadCameraConnect()
        {
            DateTime lastConnectTime = DateTime.MinValue;

            while (bThreadCameraConnect)
            {
                Thread.Sleep(10);

                DateTime now = DateTime.Now;
                DXPLAYER_STATE state = (DXPLAYER_STATE)axDXMediaPlayer1.GetState();
                if (state == DXPLAYER_STATE.STATE_STOPPED)
                {
                    double diff = (now - lastConnectTime).TotalSeconds;
                    if (diff < 0) diff = 0;
                    if (diff >= RECONNECT_INTERVAL && !bConnecting)
                    {
                        bConnecting = true;
                        lastConnectTime = now;

                        axDXMediaPlayer1.Test(0x9635371);
                        axDXMediaPlayer1.SetCommandString("FullScreenEnable", "0");
                        axDXMediaPlayer1.SetCommandString("AudioOutEnable", "0");
                        axDXMediaPlayer1.SetCommandString("TimerEnable", "0");
                        axDXMediaPlayer1.SetCommandString("DrawMode", "1");
                        axDXMediaPlayer1.SetVideoDataEvent(3);
                        axDXMediaPlayer1.SetAspectRatio(0);
                        axDXMediaPlayer1.SetDrawEnable(0);

                        if (axDXMediaPlayer1.ConnectPlayAsync(rtspURL, 1, 3, 0.0) < 0)
                            bConnecting = false;
                    }
                }
            }

            axDXMediaPlayer1.Close();
        }

        private void dxPlayer_OnAsyncResultEvent(object sender, _DDXMediaPlayerEvents_OnAsyncResultEventEvent e)
        {
            if (e.cmdtype == ASYNC_RESULT_CONNECTPLAY)
            {
                if (e.result == 0)
                {
                    Console.WriteLine("배경카메라 접속 성공");
                }
                else
                {
                    Console.WriteLine("배경카메라 접속 실패");
                }
                bConnecting = false;
            }
        }

        private void dxPlayer_OnDXMediaPlayerEvent(object sender, _DDXMediaPlayerEvents_OnDXMediaPlayerEventEvent e)
        {
            if (e.event_type == PLAYER_EVENT_STOPPED)
            {
                Console.WriteLine("배경카메라 접속 끊김");
            }
        }

        private void dxPlayer_OnVideoDataEvent(object sender, _DDXMediaPlayerEvents_OnVideoDataEventEvent e)
        {
            DateTime now = DateTime.Now;

            byte[] btaImage = new byte[e.size];

            unsafe
            {
                try
                {
                    int width = e.width;
                    int height = e.height;
                    byte* p = (byte*)e.pData;
                    Marshal.Copy((IntPtr)p, btaImage, 0, e.size);

                    Bitmap Canvas = new Bitmap(width, height);
                    BitmapData canvasData = Canvas.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadWrite, PixelFormat.Format24bppRgb);

                    IntPtr ptr = canvasData.Scan0;
                    System.Runtime.InteropServices.Marshal.Copy(btaImage, 0, ptr, (width * height * 3));
                    Canvas.UnlockBits(canvasData);

                    Canvas = GifCopyBitmap(Canvas);

                    Console.WriteLine(frame++);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                    return;
                }
            }

            DateTime end = DateTime.Now;

            Console.WriteLine(end.Subtract(now).TotalMilliseconds);
        }

        private Bitmap GifCopyBitmap(Bitmap original)
        {
            if (original == null) return null;
            try
            {
                Bitmap copy = new Bitmap(250, 200);

                using (Graphics g = Graphics.FromImage(copy))
                {
                    g.CompositingQuality = CompositingQuality.HighSpeed;
                    g.DrawImage(original, 0, 0, 250, 200);
                }
                return copy;
            }
            catch (Exception ex)
            {
                return null;
            }
        }
    }
}
