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
using System.Speech.Recognition;
using System.Speech.AudioFormat;

namespace TomKinect
{


    public partial class MainWindow : Window
    {
        KinectSensor sensor = null;
        private WriteableBitmap bitmap;
        byte[] biFrameData;
        ArrayList indexes;
        Stopwatch sw;
        KinectAudioStream converted = null;
        SpeechRecognitionEngine speechEngine = null;
        IList<Body> bodies = null;
        TextBox text = null;
        CoordinateMapper cm = null;
             
        public MainWindow()
        {
            sensor = KinectSensor.GetDefault();
            InitializeComponent();
            var depthReader = sensor.DepthFrameSource.OpenReader();
            var colorReader = sensor.ColorFrameSource.OpenReader();
            var bodyReader = sensor.BodyFrameSource.OpenReader();
            var biReader = sensor.BodyIndexFrameSource.OpenReader();

            //depthReader.FrameArrived += DepthReader_FrameArrived;
            biReader.FrameArrived += BiReader_FrameArrived;
            colorReader.FrameArrived += ColorReader_FrameArrived;
            bodyReader.FrameArrived += BodyReader_FrameArrived;

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

            var beams = sensor.AudioSource.AudioBeams[0];
            var stream = beams.OpenInputStream();
            converted = new KinectAudioStream(stream);

            IEnumerable<RecognizerInfo> recognizers = SpeechRecognitionEngine.InstalledRecognizers();
            RecognizerInfo kRecognizer = null;
            foreach(RecognizerInfo recognizer in recognizers)
            {
                string value;
                recognizer.AdditionalInfo.TryGetValue("Kinect", out value);
                if("en-US".Equals(recognizer.Culture.Name, StringComparison.OrdinalIgnoreCase))
                {
                    kRecognizer = recognizer;
                }
            }

            speechEngine = new SpeechRecognitionEngine(kRecognizer.Id);
            var gb = new GrammarBuilder {  Culture = kRecognizer.Culture };

            Choices colors = new Choices();
            colors.Add(new string[] { "red", "green", "blue" });
            gb.Append(colors);



            var g = new DictationGrammar();

            speechEngine.LoadGrammar(g);
            speechEngine.SpeechRecognized += SpeechEngine_SpeechRecognized;
            speechEngine.SpeechHypothesized += SpeechEngine_SpeechHypothesized;
            converted.SpeechActive = true;

            try
            {
                speechEngine.SetInputToAudioStream(converted, new SpeechAudioFormatInfo(EncodingFormat.Pcm, 16000, 16, 1, 32000, 2, null));
                speechEngine.RecognizeAsync(RecognizeMode.Multiple);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

            bodies = new Body[sensor.BodyFrameSource.BodyCount];
            cm = sensor.CoordinateMapper;

            text = new TextBox()
            {
                Visibility = Visibility.Visible,
                FontSize = 20.0,
            };
            text.Width = 500;
            text.Height = 50;
            canvas.Children.Add(text);

            sw = new Stopwatch();
            sw.Restart();

            sensor.Open();
        }

        private void SpeechEngine_SpeechHypothesized(object sender, SpeechHypothesizedEventArgs e)
        {
            if (e.Result != null && e.Result.Text != null)
            {
                //Console.WriteLine(e.Result.Text);
                text.Text = e.Result.Text;
            }
            converted.SpeechActive = true;
        }

        private void SpeechEngine_SpeechRecognized(object sender, SpeechRecognizedEventArgs e)
        {
            if (e.Result != null && e.Result.Text != null)
            {
                //Console.WriteLine(e.Result.Text);
                //text.Text = e.Result.Text;
            }
            converted.SpeechActive = true;
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

        private void BodyReader_FrameArrived(object sender, BodyFrameArrivedEventArgs e)
        {
            using (var frame = e.FrameReference.AcquireFrame())
            {
                if (frame != null)
                {
                    frame.GetAndRefreshBodyData(bodies);
                    for (int i = 0; i < bodies.Count; i++)
                    {
                        Body body = bodies[i];
                        if (body != null && body.IsTracked)
                        {
                            var head = body.Joints[JointType.Head].Position;
                            var pos = cm.MapCameraPointToColorSpace(head);
                            //Console.WriteLine(pos.Y);
                            Canvas.SetLeft(text, pos.X - 90);
                            Canvas.SetTop(text, pos.Y - 180);
                        }
                    }
                }
            }
        }

        private void ColorReader_FrameArrived(object sender, ColorFrameArrivedEventArgs e)
        {
            using(var frame = e.FrameReference.AcquireFrame())
            {
                if (frame != null)
                {
                    var frameDescription = frame.FrameDescription;
                    var colorFrameData = new byte[frameDescription.Width * frameDescription.Height * 4];
                    frame.CopyConvertedFrameDataToArray(colorFrameData, ColorImageFormat.Bgra);

                    var bitmap = BitmapImage.Create(frameDescription.Width, frameDescription.Height, 96d, 96d, PixelFormats.Bgr32, null, colorFrameData, 4 * frameDescription.Width);
                    image.Source = bitmap;
                }
            }
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
                            Colors.Cyan,
                            1);
                    }
                    else
                    {
                        bm.DrawLineAa(
                            newX < frameDescription.Width / 2 ? 0 : frameDescription.Width - 1,
                            newY,
                            newX,
                            newY,
                            Colors.Cyan,
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
