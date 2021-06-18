using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using System.Net.Http;
using System.IO;
using Newtonsoft.Json;
using System.Text;
using OCRAPITest.Google;
using System.Threading;
using System.Net;
using System.Collections.Generic;
using System.Collections.Specialized;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using System.Net;
using System.ComponentModel;
using SSH.Client;
using Renci.SshNet.Common;
using Renci.SshNet;


namespace OCRAPITest
{
    public partial class Form1 : Form, IDisposable
    {
        public string ImagePath { get; set; }
        byte[] list;
        Image saveImage;
        SshClient cSSH_Command;
        Thread Command_thread;
        ShellStream Command_sShell;
        public Form1()
        {
            InitializeComponent();
            cmbType.SelectedIndex = cmbLanguage.SelectedIndex = 0;
        }

        private string getSelectedLanguage()
        {
            string strLang = "";
            switch (cmbLanguage.SelectedIndex)
            {
                case 0:
                    strLang = "kor";
                    break;
            }
            return strLang;

        }

        private string getGoogleSelectedLanguage()
        {
            string strLang = "";
            switch (cmbLanguage.SelectedIndex)
            {
                case 0:
                    strLang = "ko";
                    break;
            }
            return strLang;

        }
        private void button1_Click(object sender, EventArgs e) // 파일 업로드 기능
        {
            ImagePath = ""; //이미지 경로
            pictureBox.BackgroundImage = null; //이미지 초기화
            OpenFileDialog fileDlg = new OpenFileDialog(); //파일 오픈
            fileDlg.Filter = "jpeg files|*.jpg;*.JPG;*.png;*.PNG"; //해당파일만 오픈가능하게
            if (fileDlg.ShowDialog() == DialogResult.OK)
            {
                FileInfo fileInfo = new FileInfo(fileDlg.FileName);
                if (fileInfo.Length > 1024 * 1024) //사이즈 측정 1mb넘으면 안됨.
                {
                    MessageBox.Show("jpeg file's size can not be larger than 1Mb");
                    return;
                }
                pictureBox.BackgroundImage = Image.FromFile(fileDlg.FileName); //지정한 파일에서 이미지를 만듬
                ImagePath = fileDlg.FileName; //이미지 패스 저장
                list=ImageToBase64(Image.FromFile(fileDlg.FileName), System.Drawing.Imaging.ImageFormat.Jpeg);
            }

            //옵션들 초기화
            checkBox1.Checked = false;
            saveImage = Image.FromFile(fileDlg.FileName);
        }

        private byte[] ImageToBase64(Image image, System.Drawing.Imaging.ImageFormat format) //base64로 인코딩해서 반환
        {
            using (MemoryStream ms = new MemoryStream()) //메모리 스트림 공간 할당
            {
                // Convert Image to byte[]
                image.Save(ms, format);
                byte[] imageBytes = ms.ToArray();

                return imageBytes;
            }
        }

        private async void button2_Click(object sender, EventArgs e) //api사용
        {
            RecognizeGoogleApi();
            RecognizeNaverApi();
            // RecognizeMyOCR();// 아직 FTP부분이 문제가 있음. 
        }
        public async void RecognizeNaverApi() //naver api 코드
        {
            string apiurl = "api url 입력";
            string key = "naver api 입력";

            JObject headers = new JObject(new JProperty("Content-Type", "application/json"), new JProperty("X-OCR-SECRET", key));

            JObject data = new JObject(
                new JProperty("version", "V2"),
                new JProperty("requestId", "sample_id"),
                new JProperty("timestamp", 0),
                new JProperty("images", new JArray(new JObject(new JProperty("name", "sample_image"), new JProperty("format", "jpg"), new JProperty("data", list)))));

            string str_json1=JsonConvert.SerializeObject(headers);
            string str_json2=JsonConvert.SerializeObject(data);
            
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(apiurl);
                request.ContentType = "application/json";
                request.Method = "POST";
                request.Headers.Add("X-OCR-SECRET", key);

                using (StreamWriter stream = new StreamWriter(request.GetRequestStream()))
                {
                    stream.Write(str_json2);
                    stream.Flush();
                    stream.Close();
                }

                HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                using (StreamReader reader = new StreamReader(response.GetResponseStream()))
                {
                    txtResult1.Text = "";
                    var apiResult = reader.ReadToEnd();
                    JObject res = JObject.Parse(apiResult);

                    JArray tmp = (JArray)res["images"][0]["fields"];

                for (int i = 0; i < tmp.Count; i++)
                {
                    txtResult1.Text += tmp[i]["inferText"].ToString()+"\r\n";   
                }
            }
        }

        public async void RecognizeGoogleApi() //구글 api 인식
        {
            if (string.IsNullOrEmpty(ImagePath)) //이미지가 저장되어있는지 체크 
                return;
            button1.Enabled = button2.Enabled = false; //버튼1(파일업로드) 버튼2(모름) 비활성화
            txtResult.Text = ""; //결과창 초기화
            Annotate annotate = new Annotate(); //구글 api관련 구조체였음.
            Application.DoEvents(); 
            await annotate.GetText(list, getGoogleSelectedLanguage(),cmbType.SelectedItem+"");
            if(string.IsNullOrEmpty(annotate.Error)==false) //선택된게 없을경우 에러
                MessageBox.Show("ERROR: " + annotate.Error);
            else
                txtResult.Text = annotate.TextResult; //주석을 text에 셋팅

            button1.Enabled = button2.Enabled = true; //버튼 활성화
        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            IsChecked();
        }
        private void checkBox2_CheckedChanged(object sender, EventArgs e)
        {
            IsChecked();
        }
        private void checkBox3_CheckedChanged(object sender, EventArgs e)
        {
            IsChecked();
        }
        private void checkBox4_CheckedChanged(object sender, EventArgs e)
        {
            IsChecked();
        }

        public void IsChecked()
        {
            Mat src = new Mat(ImagePath);
            if (checkBox2.Checked)
            {
                Bilateral(ref src);
            }
            if (checkBox1.Checked)
            {            
                GrayScale(ref src);
            }
            if (checkBox3.Checked)
            {
                Gradient(ref src);
            }
            if (checkBox4.Checked)
            {
                MorphGradient(ref src);
            }
            pictureBox.BackgroundImage = BitmapConverter.ToBitmap(src);
            list = ImageToBase64(pictureBox.BackgroundImage, System.Drawing.Imaging.ImageFormat.Jpeg);
            
        }
        public void GrayScale(ref Mat img) //회색화
        {
            Mat gray = new Mat();
            Mat binary = new Mat();
            Cv2.CvtColor(img, gray, ColorConversionCodes.BGR2GRAY);
            if(setting1.Text=="")
            {
                Cv2.Threshold(gray, binary, 0, 255, ThresholdTypes.Binary);
            }
            else
            {
                Cv2.Threshold(gray, binary, Convert.ToInt32(setting1.Text), 255, ThresholdTypes.Binary);
            }
            img=binary;
        }
        public void Bilateral(ref Mat img) //엣지를 보존하면서 노이즈 감소 
        {
            Mat binary = new Mat();
            Cv2.BilateralFilter(img, binary, Convert.ToInt32(setting2_1.Text), Convert.ToInt32(setting2_2.Text), Convert.ToInt32(setting2_3.Text)); // 지름, 이웃픽셀 범위, 주변픽셀 영향도
            img = binary;
        }
        public void Gradient(ref Mat img) //굵기를 이용한 노이즈 감소
        {
            Mat binary = new Mat();
            Mat element = Cv2.GetStructuringElement(MorphShapes.Ellipse, new OpenCvSharp.Size(Convert.ToInt32(setting3.Text), Convert.ToInt32(setting3.Text)));
            
            
            if (setting3_c.Checked)
            {
                Cv2.MorphologyEx(img, binary, MorphTypes.Close, element, iterations: 1); //가변수는 크기, 체크는 open이냐 close냐
            }
            else
            {
                Cv2.MorphologyEx(img, binary, MorphTypes.Open, element, iterations: 1);
            }
            img = binary;
        }
        public void MorphGradient(ref Mat img) //외곽선 검출
        {
            Mat binary = new Mat();
            Mat element = Cv2.GetStructuringElement(MorphShapes.Ellipse, new OpenCvSharp.Size(Convert.ToInt32(setting4.Text), Convert.ToInt32(setting4.Text)));

            Cv2.MorphologyEx(img, binary, MorphTypes.Gradient, element, iterations: 1); //가변수는 크기
            img = binary;
        }

        private void ImageSend()
        {
             if (!System.IO.Directory.Exists(@"C:\test"))
             {
                 System.IO.Directory.CreateDirectory(@"C:\test");
             }
             Console.WriteLine(pictureBox);
             Image tmp = pictureBox.BackgroundImage;
             FTPManager ftp = new FTPManager();
            ftp.ConnectToServer("ip", "port", "id", "pw "); //입력줘야함.
            ftp.UpLoad("image.jpg", @"C:\test\image.jpg"); 
            
        }


        private SshClient ConnectSSH()
        {
            try
            {
                SshClient cssh = new SshClient("203.234.19.102", 10784, "root", "tkdlqj1234*");
                cssh.ConnectionInfo.Timeout = TimeSpan.FromSeconds(120);

                cssh.Connect();
                return cssh;
            }
            catch (Exception ex)
            {
                return null;
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.StackTrace);
            }
        }
        private void RecvCommSSHData()
        {
            while (true)
            {
                try
                {
                    if (Command_sShell != null && Command_sShell.DataAvailable)
                    {
                        String strData = Command_sShell.Read();

                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    Console.WriteLine(ex.StackTrace);
                }

                Thread.Sleep(200);
            }
        }
        public StringBuilder SendCommand(string customCMD, ShellStream stream)
        {
            StringBuilder answer;
            var reader = new StreamReader(stream);
            var writer = new StreamWriter(stream);
            writer.AutoFlush = true;
            WriteStream(customCMD, writer, stream);
            answer = ReadStream(reader);
            return answer;
        }
        private void WriteStream(string cmd, StreamWriter writer, ShellStream stream)
        {
            writer.WriteLine(cmd);
            while (stream.Length == 0)
            {
                Thread.Sleep(500);
            }
        }
        private StringBuilder ReadStream(StreamReader reader)
        {
            StringBuilder result = new StringBuilder();
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                result.AppendLine(line);
            }
            return result;
        }
        private void RecognizeMyOCR()
        {
            ImageSend();
            cSSH_Command = ConnectSSH();

            Command_sShell = cSSH_Command.CreateShellStream("vt100", 80, 60, 800, 600, 65536);

            if (cSSH_Command.IsConnected)
            {
                Command_thread = new Thread(() => RecvCommSSHData());
                Command_thread.IsBackground = true;
                Command_thread.Start();
                txtResult2.Text = SendCommand("tesseract img.jpg stdout \\- l kor--oem 1--psm 3 \\--tessdata - dir./ workspace / output", Command_sShell).ToString();
            }
        }
    }
}