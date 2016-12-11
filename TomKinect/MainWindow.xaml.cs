using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

using Microsoft.Kinect;
using Microsoft.Kinect.Face;
using System.Net.Sockets;

using WebSocketSharp;
using System.Diagnostics;
using System.Collections;

namespace TomKinect
{


    public partial class MainWindow : Window
    {
        KinectSensor sensor = null;
        private WriteableBitmap bitmap;
        byte[] biFrameData;
        ArrayList indexes;
        Stopwatch sw;
             
        public MainWindow()
        {
            sensor = KinectSensor.GetDefault();
            InitializeComponent();
            var depthReader = sensor.DepthFrameSource.OpenReader();
            //var colorReader = sensor.ColorFrameSource.OpenReader();
            //var bodyReader = sensor.BodyFrameSource.OpenReader();
            var biReader = sensor.BodyIndexFrameSource.OpenReader();


            //var multiSourceFrameReader = sensor.OpenMultiSourceFrameReader(FrameSourceTypes.Depth | FrameSourceTypes.BodyIndex);

            //multiSourceFrameReader.MultiSourceFrameArrived += Reader_MultiSourceFrameArrived;

            depthReader.FrameArrived += DepthReader_FrameArrived;
            biReader.FrameArrived += BiReader_FrameArrived;
            //colorReader.FrameArrived += ColorReader_FrameArrived;
            //bodyReader.FrameArrived += BodyReader_FrameArrived;

            try
            {
                //var ws = new WebSocket("ws://192.168.0.9:1337");
                //ws.OnMessage += Ws_OnMessage;
                //ws.OnOpen += (sender, e) => Console.WriteLine("opened!!!!");
                //ws.OnError += (sender, e) => Console.WriteLine(e.Exception);
                //ws.OnClose += (sender, e) => Console.WriteLine("CLOSED");
                //ws.Connect();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }


            sw = new Stopwatch();
            sw.Restart();

            sensor.Open();
        }

        private void Ws_OnMessage(object sender, MessageEventArgs e)
        {
            Console.WriteLine(e.Data);
            if(e.Data.Equals("60"))
            {
                Console.WriteLine("HERE");
                sw.Restart();
            }
        }

        private void BiReader_FrameArrived(object sender, BodyIndexFrameArrivedEventArgs e)
        {
            using (var frame = e.FrameReference.AcquireFrame())
            {
                if (frame == null)
                    return;
                var frameDescription = frame.FrameDescription;
                biFrameData = new byte[frameDescription.Width * frameDescription.Height];
                frame.CopyFrameDataToArray(biFrameData);
            }
        }

        private void Reader_MultiSourceFrameArrived(object sender, MultiSourceFrameArrivedEventArgs e)
        {
            Console.WriteLine("HERE!!!");
            MultiSourceFrame msFrame = e.FrameReference.AcquireFrame();
            if (msFrame == null)
            {
                return;
            }


            var df = msFrame.DepthFrameReference.AcquireFrame();
            var biframe = msFrame.BodyIndexFrameReference.AcquireFrame();


            if (df == null || biframe == null)
                return;


            var frameDescription = df.FrameDescription;

            var depthFrameData = new ushort[frameDescription.Width * frameDescription.Height];
            var biFrameData = new byte[frameDescription.Width * frameDescription.Height];
            var pixelData = new byte[frameDescription.Width * frameDescription.Height * 4];

            df.CopyFrameDataToArray(depthFrameData);
            biframe.CopyFrameDataToArray(biFrameData);


            int pixelIndex = 0;

            for (int i = 0; i < depthFrameData.Length; i++)
            {
                var depth = depthFrameData[i];

                int intensity = depth / 2000;

                if (biFrameData[i] != 0xff)
                {
                    //Console.WriteLine("STUFF!");
                    pixelData[pixelIndex++] = (byte)(intensity);
                    pixelData[pixelIndex++] = (byte)(intensity);
                    pixelData[pixelIndex++] = (byte)(intensity);
                    pixelData[pixelIndex++] = (byte)(255);
                }
                else
                {
                    pixelData[pixelIndex++] = (byte)(0);
                    pixelData[pixelIndex++] = (byte)(0);
                    pixelData[pixelIndex++] = (byte)(0);
                    pixelData[pixelIndex++] = (byte)(255);
                }

            }

            var bitmap = BitmapImage.Create(frameDescription.Width, frameDescription.Height, 96d, 96d, PixelFormats.Bgr32, null, pixelData, 4 * frameDescription.Width);
            image.Source = bitmap;
        }

        private void BodyReader_FrameArrived(object sender, BodyFrameArrivedEventArgs e)
        {
            using (var frame = e.FrameReference.AcquireFrame())
            {
            }
            //throw new NotImplementedException();
        }

        private void ColorReader_FrameArrived(object sender, ColorFrameArrivedEventArgs e)
        {
            //throw new NotImplementedException();
        }

        private unsafe void DepthReader_FrameArrived(object sender, DepthFrameArrivedEventArgs e)
        {
            double time_elapsed = sw.Elapsed.TotalSeconds;
            if(time_elapsed > 3)
            {
                sw.Restart();
            }
           // Console.WriteLine(time_elapsed);
            using (var frame = e.FrameReference.AcquireFrame())
            {
                if (frame == null)
                    return;
   
                var frameDescription = frame.FrameDescription;
                var depthFrameData = new ushort[frameDescription.Width * frameDescription.Height];
                var pixelData = new byte[frameDescription.Width * frameDescription.Height * 4];
                var sendPixelData = new byte[pixelData.Length / 4];
                frame.CopyFrameDataToArray(depthFrameData);

                int pixelIndex = 0;
                var maxDepth = frame.DepthMaxReliableDistance;
                var minDepth = frame.DepthMinReliableDistance;

                int mapDepthToByte = maxDepth / 256;

                int interval = 10;
                indexes = new ArrayList();

                for (int i = 0; i < depthFrameData.Length; i++)
                {
                    var depth = depthFrameData[i];
                    int intensity = depth >= 0 && depth <= maxDepth ? (depth / mapDepthToByte) : 0;

                    int mask_count = 0;
                    if(biFrameData.Length > i + frameDescription.Width + 1 && i > frameDescription.Width + 1)
                    {
                        if (biFrameData[i] != 0xff)
                            mask_count += 1;
                        if (biFrameData[i + 1] != 0xff)
                            mask_count += 1;
                        if (biFrameData[i - 1] != 0xff)
                            mask_count += 1;
                        if (biFrameData[i + frameDescription.Width] != 0xff)
                            mask_count += 1;
                        if (biFrameData[i + frameDescription.Width + 1] != 0xff)
                            mask_count += 1;
                        if (biFrameData[i + frameDescription.Width - 1] != 0xff)
                            mask_count += 1;
                        if (biFrameData[i - frameDescription.Width] != 0xff)
                            mask_count += 1;
                        if (biFrameData[i - frameDescription.Width + 1] != 0xff)
                            mask_count += 1;
                        if (biFrameData[i - frameDescription.Width - 1] != 0xff)
                            mask_count += 1;
                    }
                       

                    int color = 75 + (int)((float)(i)/frameDescription.Width/frameDescription.Height*125);

                    if(mask_count > 0 && mask_count < 9)
                    {
                        indexes.Add(i);
                        if (true)
                        {
                            pixelData[pixelIndex++] = (byte)(color);
                            pixelData[pixelIndex++] = (byte)(75);
                            pixelData[pixelIndex++] = (byte)(0);
                            pixelData[pixelIndex++] = (byte)(255);
                        }
                        else
                        {
                            pixelData[pixelIndex++] = (byte)(intensity);
                            pixelData[pixelIndex++] = (byte)(intensity);
                            pixelData[pixelIndex++] = (byte)(intensity);
                            pixelData[pixelIndex++] = (byte)(255);
                        }
                    } else
                    {
                            pixelData[pixelIndex++] = (byte)(color);
                            pixelData[pixelIndex++] = (byte)(75);
                            pixelData[pixelIndex++] = (byte)(0);
                            pixelData[pixelIndex++] = (byte)(255);

                    }
                    

                }

                var bitmap = BitmapImage.Create(frameDescription.Width, frameDescription.Height, 96d, 96d, PixelFormats.Bgr32, null, pixelData, 4 * frameDescription.Width);
                WriteableBitmap bm = new WriteableBitmap(bitmap);

                var ind = indexes.ToArray();
                int newX;
                int newY;
                for(int i =0; i < indexes.Count; i+=25)
                {
                    newX = (int)ind[i] % frameDescription.Width;
                    newY = (int)((int)(ind[i]) / frameDescription.Width);
                    if (time_elapsed > 0.2)
                    {
                        bm.DrawLineAa(
                            (int)ind[indexes.Count - i - 1] % frameDescription.Width,
                            (int)((int)(ind[indexes.Count - i - 1]) / frameDescription.Width),
                            newX,
                            newY,
                            Colors.DarkSeaGreen,
                            1);
                    }
                    else
                    {
                        bm.DrawLineAa(
                            newX < frameDescription.Width / 2 ? 0 : frameDescription.Width - 1,
                            newY,
                            newX,
                            newY,
                            Colors.DarkSeaGreen,
                            1);
                    }
                }

                int width = bm.PixelWidth;
                int height = bm.PixelHeight;
                int stride = bm.BackBufferStride;
                int bytesPerPixel = (bm.Format.BitsPerPixel + 7) / 8;

                bm.Lock();
                byte* pBuff = (byte*)bm.BackBuffer;


                for (int i = 0; i < depthFrameData.Length; i++)
                {
                    if(false)//biFrameData[i] == 0xff && time_elapsed > 0.2) //lasers inside body
                    {
                        newX = (int)i % frameDescription.Width;
                        newY = (int)((int)(i / frameDescription.Width));

                        pBuff[4 * newX + (newY * stride)] = 0;
                        pBuff[4 * newX + (newY * stride) + 1] = 0;
                        pBuff[4 * newX + (newY * stride) + 2] = 0;
                        pBuff[4 * newX + (newY * stride) + 3] = 255;
                    }
                }
                bm.Unlock();
                image.Source = bm;
            }
        }
    }
}
