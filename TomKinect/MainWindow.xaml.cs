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

namespace TomKinect
{


    public partial class MainWindow : Window
    {
        KinectSensor sensor = null;
        public MainWindow()
        {
            sensor = KinectSensor.GetDefault();
            InitializeComponent();

            var depthReader = sensor.DepthFrameSource.OpenReader();
            depthReader.FrameArrived += DepthReader_FrameArrived;
            sensor.Open();
        }

        private void DepthReader_FrameArrived(object sender, DepthFrameArrivedEventArgs e)
        {
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

                for (int i = 0; i < depthFrameData.Length; i++)
                {
                    var depth = depthFrameData[i];
                    int intensity = depth >= 0 && depth <= maxDepth ? (depth / mapDepthToByte) : 0;

                    if(i % interval == 0)
                    {
                        if (depth > 2000)
                        {
                            pixelData[pixelIndex++] = (byte)(0);
                            pixelData[pixelIndex++] = (byte)(0);
                            pixelData[pixelIndex++] = (byte)(0);
                            pixelData[pixelIndex++] = (byte)(255);
                            sendPixelData[i] = (byte)(0);
                        }
                        else
                        {
                            pixelData[pixelIndex++] = (byte)(intensity);
                            pixelData[pixelIndex++] = (byte)(intensity);
                            pixelData[pixelIndex++] = (byte)(intensity);
                            pixelData[pixelIndex++] = (byte)(255);
                            sendPixelData[i] = (byte)(1);
                        }
                    } else
                    {
                            pixelData[pixelIndex++] = (byte)(0);
                            pixelData[pixelIndex++] = (byte)(0);
                            pixelData[pixelIndex++] = (byte)(0);
                            pixelData[pixelIndex++] = (byte)(255);

                    }

                }

                //var pixels = new byte[]
                //for(int i = 0; i < pixelData.Length; i++)
                //{

                //}

                var compressed = new List<byte>();
                var state = 0; // statee
                int counter = 0;
                for(var i = 0; i < sendPixelData.Length; i++)
                {
                    var curVal = sendPixelData[i];
                    if(state == curVal) 
                    {
                        counter++;
                    }
                    else
                    {
                        if(state == 0)
                        {
                            compressed.Add((byte)(-counter));
                        } else
                        {
                            compressed.Add((byte)counter);
                        }
                        state = curVal;
                        counter = 1;
                    }
                }

                UdpClient udpClient = new UdpClient("9.66.209.194", 33333);
                try
                {
                    Console.WriteLine(sendPixelData.Length + "-----------");
                    //udpClient.Send(sendPixelData, sendPixelData.Length);
                    udpClient.Send(compressed.ToArray(), compressed.Count);
                    //Byte[] sendBytes = Encoding.ASCII.GetBytes("HELLLLOOOOOO");
                    //udpClient.Send(sendBytes, sendBytes.Length);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                }

                var bitmap = BitmapImage.Create(frameDescription.Width, frameDescription.Height, 96d, 96d, PixelFormats.Bgr32, null, pixelData, 4 * frameDescription.Width);
                image.Source = bitmap;
            }
        }
    }
}
