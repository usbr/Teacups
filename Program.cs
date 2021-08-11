using System;
using System.Drawing;
using System.Text;
using System.IO;
using System.Net;
using PdfSharp;

namespace Teacup
{
    static class Program
    {    
        static string mainDataFile;
        
        static void Main(string[] args)
        {
            //Read the config file
            string[] inputItems = System.IO.File.ReadAllLines("teacups.cfg");
            var bmp = BuildTeacupCharts(inputItems);

            // Save image file
            bmp.Save("usbrTeacups.bmp");

            // Save bmp to pdf
            // Create a new PDF document
            PdfSharp.Pdf.PdfDocument document = new PdfSharp.Pdf.PdfDocument();
            // Create an empty page
            PdfSharp.Pdf.PdfPage page = document.AddPage();
            page.Orientation = PageOrientation.Landscape;
            double width = page.Width;
            double height = page.Height;
            // Get an XGraphics object for drawing
            PdfSharp.Drawing.XGraphics gfx = PdfSharp.Drawing.XGraphics.FromPdfPage(page);
            PdfSharp.Drawing.XImage image = PdfSharp.Drawing.XImage.FromFile("usbrTeacups.bmp");
            gfx.DrawImage(image, 0, 0, width, height);
            document.Save("usbrTeacups.pdf");
        }

        /// <summary>
        /// Builds the teacup maps and charts
        /// </summary>
        /// <param name="lines"></param>
        private static Bitmap BuildTeacupCharts(string[] lines)
        {
            ServicePointManager.Expect100Continue = true;
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            // Grab base image file  
            Bitmap bmp;
            try
            {
                var imgURL = @"file://ibr2mprgis005.bor.doi.net/WebApps/DroughtBaseMap/DroughtBaseMap.png";
                WebClient client = new WebClient();
                Stream stream = client.OpenRead(imgURL);
                var gif = new Bitmap(stream);
                stream.Flush();
                stream.Close();
                client.Dispose();
                bmp = new Bitmap(gif);
            }
            catch
            {
                var gif = Image.FromFile("basePicture.png");
                bmp = new Bitmap(gif);
            }
            // Draw image header
            PointF headerLocation = new PointF(2f, 3f);
            string headerText = "\t\t\t\tCurrent Reservoir Storage as of " + DateTime.Now.AddDays(-1).Date.ToString("MMMM d, yyyy") + 
                "\n\t\t\t\tMajor Reclamation Reservoirs";
            Font headerFont = new Font("SegoeUI", 62, FontStyle.Bold);
            using (Graphics graphics = Graphics.FromImage(bmp))
            {
                graphics.FillRectangle(Brushes.Transparent, new Rectangle(0, 0, 100, 20));
                graphics.DrawString(headerText, headerFont, Brushes.White, headerLocation);
            }

            // Get DataFile from USBR public web
            var dataURL = "https://www.usbr.gov/lc/region/g4000/riverops/webreports/dailyDroughtReservoirData.txt";
            mainDataFile = (new WebClient()).DownloadString(dataURL);

            //Main loop of the program
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                if (line[0].ToString() != "#")
                {
                    var cfg = new ConfigLine(line);
                    if (cfg.IsTeacup)
                    {
                        if (cfg.output == "all")
                        {
                            drawTeacupMap(bmp, cfg);
                        }
                        if (cfg.output == "all" || cfg.output == "standalone")
                        {
                            generateStandAloneTeacupImage(cfg);
                        }
                    }
                }
            }
            return bmp;
        }


        /// <summary>
        /// Draws and labels the teacup on the image file
        /// </summary>
        /// <param name="bmp"></param>
        /// <param name="cfg"></param>
        private static void drawTeacupMap(Bitmap bmp, ConfigLine cfg)
        {
            var results = getData(cfg);
            try
            {
                Console.Write("Processing " + results[0] + "... ");
                // Extract label value
                double volvalue, avgvalue;
                DateTime tvalue = new DateTime();
                try
                {
                    volvalue = Convert.ToDouble(results[1].ToString().Trim());
                    avgvalue = Convert.ToDouble(results[2].ToString().Trim());
                    tvalue = DateTime.Parse(results[3].ToString().Trim());
                }
                catch
                {
                    volvalue = double.NaN;
                    avgvalue = double.NaN;
                }

                //Determine the percent full
                int full = GetTeacupLevelValue(volvalue, cfg);
                //Determine the average full
                int full2 = GetTeacupLevelValue(avgvalue, cfg);

                //check for missing values and set the output number of digits to report
                string number = "", number2 = "", Percent = "", Percent2 = "";
                if (double.IsNaN(volvalue))
                {
                    number = "MISSING";
                    number2 = "MISSING";
                    Percent = "MISSING";
                    Percent2 = "MISSING";
                }
                else
                {
                    number = (volvalue).ToString("N0") + "";
                    number2 = (avgvalue).ToString("N0") + " ac-ft";
                    Percent = (Math.Max(0.0, Math.Min(volvalue / cfg.capacity, 1.0)) * 100).ToString("F0") + "% Full";
                    Percent2 = (100 * volvalue / avgvalue).ToString("F0") + "% Avg";
                }

                //Create Isosceles trapizoid
                string Text = InsertSpaceBeforeUpperCase(cfg.DisplayName) + "\n" +
                    "Storage: " + number + " / " + (cfg.capacity).ToString("N0") + " ac-ft" + "\n" +
                    Percent + " - " + Percent2 + "\n" +
                    "Data as of " + tvalue.ToString("MMM d, yyyy");
                Point Location = new Point(cfg.col + 75, cfg.row + 5);
                StringFormat stringFormat = new StringFormat();
                stringFormat.Alignment = StringAlignment.Center;

                //Line color and size
                Font teacupFont = new Font("SegoeUI", 24, FontStyle.Bold);
                Pen blackPen = new Pen(Color.Black, 4);
                Pen redPen = new Pen(Color.Red, 6);
                redPen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;
                //Fill Color for background
                SolidBrush whiteBrush = new SolidBrush(Color.White);
                SolidBrush blueBrush = new SolidBrush(Color.RoyalBlue);

                //Setting the points of the trapezoid
                Point point1 = new Point(cfg.col, cfg.row); //lower left
                Point point2 = new Point(cfg.col + 10 * cfg.size, cfg.row); //lower right
                Point point3 = new Point(cfg.col + 20 * cfg.size, cfg.row - 20 * cfg.size); //upper right
                Point point4 = new Point(cfg.col - 10 * cfg.size, cfg.row - 20 * cfg.size); //upper left
                Point[] curvePoints = { point1, point2, point3, point4 };

                //setting points of percent full
                Point point1f = new Point(cfg.col, cfg.row); //lower left
                Point point2f = new Point(cfg.col + 10 * cfg.size, cfg.row); //lower right
                Point point3f = new Point(cfg.col + 10 * cfg.size + cfg.size * full * 10 / 100, cfg.row - 20 * cfg.size * full / 100); //upper right
                Point point4f = new Point(cfg.col - 10 * cfg.size * full / 100, cfg.row - 20 * cfg.size * full / 100); //upper left
                Point[] fullPoints = { point1f, point2f, point3f, point4f };

                //setting points of average full
                Point point3a = new Point(cfg.col + 10 * cfg.size + cfg.size * full2 * 10 / 100, cfg.row - 20 * cfg.size * full2 / 100); //upper right
                Point point4a = new Point(cfg.col - 10 * cfg.size * full2 / 100, cfg.row - 20 * cfg.size * full2 / 100); //upper left                

                //Create Graphics
                using (Graphics graphics = Graphics.FromImage(bmp))
                {
                    // Base teacup
                    graphics.DrawPolygon(blackPen, curvePoints);
                    graphics.FillPolygon(whiteBrush, curvePoints);
                    // Filled portion
                    graphics.FillPolygon(blueBrush, fullPoints);
                    // Average line
                    graphics.DrawLine(redPen, point3a, point4a);
                    // Label
                    graphics.DrawString(Text, teacupFont, Brushes.Black, Location, stringFormat);
                }
                Console.WriteLine("OK!");
            }
            catch
            {
                Console.WriteLine("FAIL!");
            }
        }


        /// <summary>
        /// Builds a stand-alone teacup image file
        /// </summary>
        /// <param name="cfg"></param>
        private static void generateStandAloneTeacupImage(ConfigLine cfg)
        {
            cfg.size = 4;
            cfg.row = 90;
            cfg.col = 80;
            var fName = AppDomain.CurrentDomain.BaseDirectory + cfg.DisplayName.Replace(" ", "") + ".png";
            int width = 200, height = 150;
            var bmp = new Bitmap(width, height);

            var results = getData(cfg);
            try
            {
                Console.Write("Processing Stand-Alone " + results[0] + "... ");
                // Extract label value
                double volvalue, avgvalue, lovalue, hivalue;
                DateTime tvalue = new DateTime();
                try
                {
                    volvalue = Convert.ToDouble(results[1].ToString().Trim());
                    avgvalue = Convert.ToDouble(results[2].ToString().Trim());
                    tvalue = DateTime.Parse(results[3].ToString().Trim());
                    lovalue = Convert.ToDouble(results[4].ToString().Trim());
                    hivalue = Convert.ToDouble(results[5].ToString().Trim());
                }
                catch
                {
                    volvalue = double.NaN;
                    avgvalue = double.NaN;
                    lovalue = double.NaN;
                    hivalue = double.NaN;
                }

                //Determine the percent full
                int full = GetTeacupLevelValue(volvalue, cfg);
                //Determine the average full
                int fullAvg = GetTeacupLevelValue(avgvalue, cfg);
                //Determine the average full
                int fullLo = GetTeacupLevelValue(lovalue, cfg);
                //Determine the average full
                int fullHi = GetTeacupLevelValue(hivalue, cfg);

                //check for missing values and set the output number of digits to report
                string number = "", number2 = "", Percent = "", Percent2 = "";
                if (double.IsNaN(volvalue))
                {
                    number = "MISSING";
                    number2 = "MISSING";
                    Percent = "MISSING";
                    Percent2 = "MISSING";
                }
                else
                {
                    number = (volvalue).ToString("N0") + "";
                    number2 = (avgvalue).ToString("N0") + " ac-ft";
                    Percent = (Math.Max(0.0, Math.Min(volvalue / cfg.capacity, 1.0)) * 100).ToString("F0") + "% Full";
                    Percent2 = (100 * volvalue / avgvalue).ToString("F0") + "% Avg";
                }

                //Create Isosceles trapizoid
                string Text = InsertSpaceBeforeUpperCase(cfg.DisplayName) + "\n" +
                    "" + number + " / " + (cfg.capacity).ToString("N0") + " ac-ft" + "\n" +
                    Percent + " - " + Percent2 + "\n" +
                    "" + tvalue.ToString("MMM d, yyyy");
                Point Location = new Point(cfg.col + 20, cfg.row + 5);
                StringFormat stringFormat = new StringFormat();
                stringFormat.Alignment = StringAlignment.Center;

                //Line color and size
                Font teacupFont = new Font("Carbon", 8, FontStyle.Regular);
                Pen blackPen = new Pen(Color.Black, 3);
                Pen redPen = new Pen(Color.Red, 2);
                redPen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;
                Pen orangePen = new Pen(Color.Goldenrod, 2);
                orangePen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dot;
                //Fill Color for background
                SolidBrush whiteBrush = new SolidBrush(Color.White);
                SolidBrush blueBrush = new SolidBrush(Color.RoyalBlue);

                //Setting the points of the trapezoid
                Point point1 = new Point(cfg.col, cfg.row); //lower left
                Point point2 = new Point(cfg.col + 10 * cfg.size, cfg.row); //lower right
                Point point3 = new Point(cfg.col + 20 * cfg.size, cfg.row - 20 * cfg.size); //upper right
                Point point4 = new Point(cfg.col - 10 * cfg.size, cfg.row - 20 * cfg.size); //upper left
                Point[] curvePoints = { point1, point2, point3, point4 };

                //setting points of percent full
                Point point1f = new Point(cfg.col, cfg.row); //lower left
                Point point2f = new Point(cfg.col + 10 * cfg.size, cfg.row); //lower right
                Point point3f = new Point(cfg.col + 10 * cfg.size + cfg.size * full * 10 / 100, cfg.row - 20 * cfg.size * full / 100); //upper right
                Point point4f = new Point(cfg.col - 10 * cfg.size * full / 100, cfg.row - 20 * cfg.size * full / 100); //upper left
                Point[] fullPoints = { point1f, point2f, point3f, point4f };

                //setting points of average full
                Point point3a = new Point(cfg.col + 10 * cfg.size + cfg.size * fullAvg * 10 / 100, cfg.row - 20 * cfg.size * fullAvg / 100); //upper right
                Point point4a = new Point(cfg.col - 10 * cfg.size * fullAvg / 100, cfg.row - 20 * cfg.size * fullAvg / 100); //upper left  

                //setting points of low full
                Point point3l = new Point(cfg.col + 10 * cfg.size + cfg.size * fullLo * 10 / 100, cfg.row - 20 * cfg.size * fullLo / 100); //upper right
                Point point4l = new Point(cfg.col - 10 * cfg.size * fullLo / 100, cfg.row - 20 * cfg.size * fullLo / 100); //upper left 

                //setting points of high full
                Point point3h = new Point(cfg.col + 10 * cfg.size + cfg.size * fullHi * 10 / 100, cfg.row - 20 * cfg.size * fullHi / 100); //upper right
                Point point4h = new Point(cfg.col - 10 * cfg.size * fullHi / 100, cfg.row - 20 * cfg.size * fullHi / 100); //upper left               

                //Create Graphics
                using (Graphics graphics = Graphics.FromImage(bmp))
                {
                    graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
                    // Border
                    graphics.DrawLine(new Pen(Brushes.Black, 2), new Point(0, 0), new Point(0, height));
                    graphics.DrawLine(new Pen(Brushes.Black, 2), new Point(0, 0), new Point(width, 0));
                    graphics.DrawLine(new Pen(Brushes.Black, 2), new Point(0, height), new Point(width, height));
                    graphics.DrawLine(new Pen(Brushes.Black, 2), new Point(width, 0), new Point(width, height));
                    // Base teacup
                    graphics.DrawPolygon(blackPen, curvePoints);
                    graphics.FillPolygon(whiteBrush, curvePoints);
                    // Filled portion
                    graphics.FillPolygon(blueBrush, fullPoints);
                    // Average line
                    graphics.DrawLine(redPen, point3a, point4a);
                    graphics.DrawLine(orangePen, point3l, point4l);
                    graphics.DrawLine(orangePen, point3h, point4h);
                    // Label
                    graphics.DrawString(Text, teacupFont, Brushes.Black, Location, stringFormat);
                }
                bmp.Save(fName);
                Console.WriteLine("OK!");
            }
            catch
            {
                Console.WriteLine("FAIL!");
            }
        }


        /// <summary>
        /// Calculates the teacup level using the input value, the max teacup capacity, and an area calculation
        /// </summary>
        /// <param name="inputValue"></param>
        /// <param name="cfg"></param>
        /// <returns></returns>
        private static int GetTeacupLevelValue(double inputValue, ConfigLine cfg)
        {
            double percent;
            if (double.IsNaN(inputValue))
            {
                percent = 0;
            }
            else
                percent = inputValue / cfg.capacity;

            if (percent >= 1)
            {
                percent = 1;
            }
            else if (percent <= 0)
            {
                percent = 0;
            }
            double area2 = 400 * cfg.size * cfg.size + 3200 * cfg.size * cfg.size * percent;
            area2 = Math.Sqrt(area2) - 20 * cfg.size;
            area2 = area2 / (40 * cfg.size);
            if (area2 >= 1.000)
            {
                area2 = 1.000;
            }
            if (area2 <= 0.000)
            {
                area2 = 0.000;
            }
            int fullValue= Convert.ToInt32(area2 * 100);
            return fullValue;
        }
        

        /// <summary>
        /// Gets data from the specified static data file
        /// </summary>
        /// <param name="cfg"></param>
        /// <returns></returns>
        private static string[] getData(ConfigLine cfg)
        {
            var resName = cfg.ResName.ToLower();
            string[] results = new string[] { };
            foreach (var item in mainDataFile.Split(new[] { Environment.NewLine },StringSplitOptions.None))
            {
                var itemArray = item.Split(',');
                var itemName = itemArray[0].ToString().Replace(" ","").ToLower();
                if (resName == itemName)
                {
                    // item-name, data-value, data-average, display-name, data-10P, data-90P
                    results = new string[] { itemName, itemArray[7], itemArray[17], itemArray[8], itemArray[15], itemArray[11] };
                    return results;
                }
            }
            return results;
        }


        /// <summary>
        /// Inserts a space before an upper-case or dash character for display purposes
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static string InsertSpaceBeforeUpperCase(string str)
        {
            var sb = new StringBuilder();
            char previousChar = char.MinValue; // Unicode '\0'
            foreach (char c in str)
            {
                if (char.IsUpper(c) || c == '-')
                {
                    // If not the first character and previous character is not a space, insert a space before uppercase
                    if (sb.Length != 0 && previousChar != ' ')
                    {
                        sb.Append(' ');
                    }
                }
                sb.Append(c);
                previousChar = c;
            }
            return sb.ToString();
        }


    }
}
