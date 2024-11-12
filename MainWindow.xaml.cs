using Microsoft.Win32;
using nom.tam.fits;
using nom.tam.util;
using System.Diagnostics;
using System.Security.Policy;
using System.Security.RightsManagement;
using System.Windows;
using System.Windows.Controls;

namespace BiasDarksFlats
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            loadBiasImages.Visibility = Visibility.Hidden;
            lowBiasSliderPanel.Visibility = Visibility.Hidden;
            highBiasSliderPanel.Visibility = Visibility.Hidden;
            averagingMethodBias.Visibility = Visibility.Hidden;

            createMasterDark.Visibility = Visibility.Hidden;
            lowDarksSliderPanel.Visibility = Visibility.Hidden;
            highDarksSliderPanel.Visibility = Visibility.Hidden;
            averagingMethodDarks.Visibility = Visibility.Hidden;

            loadFlatsImages.Visibility = Visibility.Hidden;
            lowFlatsSliderPanel.Visibility = Visibility.Hidden;
            highFlatsSliderPanel.Visibility = Visibility.Hidden;
            averagingMethodFlats.Visibility = Visibility.Hidden;
            normalizationMethodFlats.Visibility = Visibility.Hidden;

            // tbHello.Text = "Hello";
        }

        private void loadBias_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog biasFileDialog = new OpenFileDialog();
            biasFileDialog.Filter = "Bias Files |*.fit;*.fits";
            biasFileDialog.Title = "Please pick all your Bias files...";
            biasFileDialog.Multiselect = true;

            bool? biasSelectedSuccess = biasFileDialog.ShowDialog();

            if (biasSelectedSuccess == true)
            {
                // biasFilesList
                string[] biasFilePaths = biasFileDialog.FileNames;

                foreach (string biasFile in biasFilePaths)
                {
                    biasFilesList.Items.Add(biasFile);
                }

                loadBiasImages.Visibility = Visibility.Visible;
                lowBiasSliderPanel.Visibility = Visibility.Visible;
                highBiasSliderPanel.Visibility = Visibility.Visible;
                averagingMethodBias.Visibility = Visibility.Visible;
            }
            else
            {
                // No files selected
                MessageBox.Show("No Bias Files Selected", "No File Selected", MessageBoxButton.OK, MessageBoxImage.Asterisk);
            }
        }
        static private int[] SortArray(int[] array, int leftIndex, int rightIndex)
        {
            var i = leftIndex;
            var j = rightIndex;
            var pivot = array[leftIndex];
            while (i <= j)
            {
                while (array[i] < pivot)
                {
                    i++;
                }

                while (array[j] > pivot)
                {
                    j--;
                }
                if (i <= j)
                {
                    int temp = array[i];
                    array[i] = array[j];
                    array[j] = temp;
                    i++;
                    j--;
                }
            }

            if (leftIndex < j)
                SortArray(array, leftIndex, j);
            if (i < rightIndex)
                SortArray(array, i, rightIndex);
            return array;
        }

        static private int[] Winsorize(int[] a, double low_limit, double up_limit, bool low_include = true, bool up_include = true)
        {
            /*
            n = a.count() // I think this is the number of non masked elements
            idx = a.argsort()
            if contains_nan:
                nan_count = np.count_nonzero(np.isnan(a))
            if low_limit:
                if low_include:
                    lowidx = int(low_limit * n)
                else:
                    lowidx = np.round(low_limit * n).astype(int)
                if contains_nan and nan_policy == 'omit':
                    lowidx = min(lowidx, n - nan_count - 1)
                a[idx[:lowidx]] = a[idx[lowidx]]
            if up_limit is not None:
                if up_include:
                    upidx = n - int(n * up_limit)
                else:
                    upidx = n - np.round(n * up_limit).astype(int)
                if contains_nan and nan_policy == 'omit':
                    a[idx[upidx: -nan_count]] = a[idx[upidx - 1]]
                else:
                    a[idx[upidx:]] = a[idx[upidx - 1]]
            return a
            */
            int n = a.Length;
            int[] idx = new int[n];
            idx = SortArray(a, 0, a.Length - 1);

            if (low_limit > 0)
            {
                int lowidx = 0;
                if (low_include)
                    lowidx = (int)((double)low_limit * (double)n);
                else
                    lowidx = (int)Math.Round((double)low_limit * (double)n);

                for (int j = 0; j < lowidx; j++)
                {
                    idx[j] = a[lowidx];
                }
            }

            if (up_limit > 0)
            {
                int upidx = n;
                if (low_include)
                    upidx = n - (int)((double)up_limit * (double)n);
                else
                    upidx = n - (int)Math.Round((double)up_limit * (double)n);

                for (int j = upidx; j < n; j++)
                {
                    idx[j] = a[upidx - 1];
                }
            }

            return a;
        }

        static private int WinsorizedMean(int[] a, double low_limit, double up_limit, bool low_include = true, bool up_include = true)
        {
            int[] winsorizedArray = Winsorize(a, low_limit, up_limit, low_include, up_include);

            int arraySum = 0;

            for (int i = 0; i < winsorizedArray.Length; i++)
            {
                arraySum += winsorizedArray[i];
            }

            int winsorizedMean = (int)((float)arraySum / (float)winsorizedArray.Length);

            return winsorizedMean;
        }

        static private (int[,,], int[], string) LoadImageStack(ItemCollection filnamesList)
        {
            // string filename = "";
            int numberOfBiasImages = filnamesList.Count;
            int imageCounter = 0;

            int[,,] imageStack = null;
            List<int[,]> imageList = null;
            int[] imageDimensions = null;
            string firstFilename = null;

            foreach (String item in filnamesList)
            {
                string fitsFilename = "";
                fitsFilename = item.ToString();
                if (firstFilename == null)
                {
                    firstFilename = fitsFilename;
                }

                Fits f = new Fits(fitsFilename);
                int iHDU = 0;
                BasicHDU h;

                h = f.ReadHDU();
                if (h != null)
                {
                    if (iHDU == 0)
                    {
                        System.Console.Out.WriteLine("\n\nPrimary header:\n");
                    }
                    else
                    {
                        System.Console.Out.WriteLine("\n\nExtension " + iHDU + ":\n");
                    }
                    iHDU += 1;
                    h.Info();

                    System.Array[] img_array = (System.Array[])h.Data.Kernel;

                    imageDimensions = ArrayFuncs.GetDimensions(img_array);
                    if (imageStack == null)
                    {
                        imageStack = new int[numberOfBiasImages, imageDimensions[0], imageDimensions[1]];
                    }
                    if (imageList == null)
                    {
                        imageList = new List<int[,]>();
                    }
                    int[,] imageCopy = new int[imageDimensions[0], imageDimensions[1]];

                    for (int i = 0; i < imageDimensions[0]; i++)
                    {
                        System.Array row = img_array[i];
                        short[] arrayInteger = img_array[i].Cast<short>().ToArray();

                        for (int j = 0; j < imageDimensions[1]; j++)
                        {
                            imageCopy[i, j] = (int)(arrayInteger[j] + 32768);
                            imageStack[imageCounter, i, j] = (int)(arrayInteger[j] + 32768);
                        }

                        imageList.Add(imageCopy);
                    }
                }

                imageCounter += 1;
            }

            return (imageStack, imageDimensions, firstFilename);
        }

        static private (int[,], int[,]) WinsorizeStack(int[,,] imageStack, int[] imageDimensions, double lowLimit, double upperLimit)
        {
            int numberOfStackedImages = imageStack.GetLength(0);
            int[,] imageMean = new int[imageDimensions[0], imageDimensions[1]];
            int[,] imageWinsorizedMean = new int[imageDimensions[0], imageDimensions[1]];

            for (int i = 0; i < imageDimensions[0]; i++)
            {
                for (int j = 0; j < imageDimensions[1]; j++)
                {
                    int pixelSum = 0;

                    int[] arrayToBeWinsorized = new int[numberOfStackedImages];

                    for (int imageNumber = 0; imageNumber < numberOfStackedImages; imageNumber++)
                    {
                        int pixelValue = imageStack[imageNumber, i, j];
                        pixelSum += (int)pixelValue;
                        arrayToBeWinsorized[imageNumber] = pixelValue;
                    }

                    int pixelAverage = (int)((float)pixelSum / (float)numberOfStackedImages);

                    int d = 1;

                    imageMean[i, j] = pixelAverage;

                    // var sortFunction = new QuickSortMethods();
                    // var sortedArray = SortArray(arrayToBeWinsorized, 0, arrayToBeWinsorized.Length - 1);
                    int winsorizedMean = (int)WinsorizedMean(arrayToBeWinsorized, lowLimit, upperLimit);
                    // const short shortOffset = 32768;
                    imageWinsorizedMean[i, j] = (short)((int)winsorizedMean - (int)32768);
                    // imageWinsorizedMean[i, j] = (short)((short)winsorizedMean);
                }
            }

            return (imageWinsorizedMean, imageMean);
        }

        static private int[,] MeanOfStack(int[,,] imageStack, int[] imageDimensions)
        {
            int numberOfStackedImages = imageStack.GetLength(0);
            int[,] imageMean = new int[imageDimensions[0], imageDimensions[1]];
            // int[,] imageWinsorizedMean = new int[imageDimensions[0], imageDimensions[1]];

            for (int i = 0; i < imageDimensions[0]; i++)
            {
                for (int j = 0; j < imageDimensions[1]; j++)
                {
                    int pixelSum = 0;

                    // int[] arrayToBeWinsorized = new int[numberOfStackedImages];

                    for (int imageNumber = 0; imageNumber < numberOfStackedImages; imageNumber++)
                    {
                        int pixelValue = imageStack[imageNumber, i, j];
                        pixelSum += (int)pixelValue;
                        // arrayToBeWinsorized[imageNumber] = pixelValue;
                    }

                    int pixelAverage = (int)((float)pixelSum / (float)numberOfStackedImages);

                    // int d = 1;

                    imageMean[i, j] = pixelAverage;

                    // var sortFunction = new QuickSortMethods();
                    // var sortedArray = SortArray(arrayToBeWinsorized, 0, arrayToBeWinsorized.Length - 1);
                    // int winsorizedMean = (int)WinsorizedMean(arrayToBeWinsorized, 0.1f, 0.2f);
                    // const short shortOffset = 32768;
                    // imageWinsorizedMean[i, j] = (short)((int)winsorizedMean - (int)32768);
                    // imageWinsorizedMean[i, j] = (short)((short)winsorizedMean);
                }
            }

            return imageMean;
        }

        static private int[,] MedianOfStack(int[,,] imageStack, int[] imageDimensions)
        {
            int numberOfStackedImages = imageStack.GetLength(0);
            int[,] imageMean = new int[imageDimensions[0], imageDimensions[1]];
            // int[,] imageWinsorizedMean = new int[imageDimensions[0], imageDimensions[1]];

            for (int i = 0; i < imageDimensions[0]; i++)
            {
                for (int j = 0; j < imageDimensions[1]; j++)
                {
                    int pixelSum = 0;

                    int[] medianArray = new int[numberOfStackedImages];

                    for (int imageNumber = 0; imageNumber < numberOfStackedImages; imageNumber++)
                    {
                        int pixelValue = imageStack[imageNumber, i, j];
                        // pixelSum += (int)pixelValue;
                        medianArray[imageNumber] = pixelValue;
                    }

                    int pixelMedian = (int) MedianOf1DArray(medianArray);

                    // int d = 1;

                    imageMean[i, j] = pixelMedian;

                    // var sortFunction = new QuickSortMethods();
                    // var sortedArray = SortArray(arrayToBeWinsorized, 0, arrayToBeWinsorized.Length - 1);
                    // int winsorizedMean = (int)WinsorizedMean(arrayToBeWinsorized, 0.1f, 0.2f);
                    // const short shortOffset = 32768;
                    // imageWinsorizedMean[i, j] = (short)((int)winsorizedMean - (int)32768);
                    // imageWinsorizedMean[i, j] = (short)((short)winsorizedMean);
                }
            }

            return imageMean;
        }

        static private void SaveFITSImage(string representativeFilename, string saveFilename, int[,] arrayToBeSaved, int[] imageDimensions)
        {
            Fits firstFITS = new Fits(representativeFilename);
            int firstHDU = 0;
            BasicHDU firsth;

            firsth = firstFITS.ReadHDU();
            if (firsth != null)
            {
                System.Array[] img_array = (System.Array[])firsth.Data.Kernel;
                // short[][] short_array = (short[][])firsth.Data.Kernel;

                for (int i = 0; i < imageDimensions[0]; i++)
                {
                    short[] row = (short[])img_array[i];


                    short[] newRow = { 1, 2, 3 };
                    img_array[i] = newRow;
                    // short[] wRow = imageWinsorizedMean[i];
                    // ((System.Array[])firsth.Data.Kernel)[i] = newRow;

                    // System.Array img_array = (System.Array)firsth.Data.Kernel[i];

                    short[] currentRow = new short[imageDimensions[1]];

                    for (int j = 0; j < imageDimensions[1]; j++)
                    {
                        // row[j] = short i;
                        // img_array[i][j] = (short)(imageWinsorizedMean[i, j] - 32768);
                        // (short)((System.Array)((System.Array[])firsth.Data.Kernel)[i])[j] = (short)0;
                        currentRow[j] = (short)arrayToBeSaved[i, j];
                        // currentRow[j] = (short) j;
                    }

                    ((System.Array[])firsth.Data.Kernel)[i] = currentRow;
                }

                // int asdf = 1;

                /*
                nom.tam.fits.Fits fits = new nom.tam.fits.Fits();
                nom.tam.fits.BasicHDU hdu = nom.tam.fits.Fits.MakeHDU(imageWinsorizedMean);
                hdu.AddValue("BITPIX", 16, null);  // set bit depth of 16 bit
                hdu.AddValue("NAXIS", 2, null);    // 2D-image
                fits.AddHDU(hdu);                  // Debugging here shows correct HDU-data, i.e. gradient from top left to bottom right
                nom.tam.util.BufferedFile file = new nom.tam.util.BufferedFile(@"C:\\Users\\Scott\\source\\repos\\BiasDarksFlats\\test.fits", System.IO.FileAccess.ReadWrite, System.IO.FileShare.ReadWrite);
                fits.Write(file);
                
                file.Flush();
                file.Close();
                */

                nom.tam.util.BufferedFile file = new nom.tam.util.BufferedFile(saveFilename, System.IO.FileAccess.ReadWrite, System.IO.FileShare.ReadWrite);

                firstFITS.Write(file);
                file.Flush();
                file.Close();

                int asdf = 1;
            }

            return;
        }

        private void loadBiasImages_Click(object sender, RoutedEventArgs e)
        {
            (int[,,] imageStack, int[] imageDimensions, string firstFilename) = LoadImageStack(biasFilesList.Items);
            // string filename = "";

            int numberOfBiasImages = biasFilesList.Items.Count;
            /*
            int imageCounter = 0;

            // Test the winsorize algorithm
            // ushort[] testWinsorize = new ushort[] { 10, 4, 9, 8, 5, 3, 7, 2, 1, 6 };
            // ushort[] winsorizedArray = Winsorize(testWinsorize, 0.1f, 0.2f);

            // int[,,] imageStack = null;
            List<int[,]> imageList = null;
            int[] imageDimensions = null;
            string firstFilename = null;

            foreach (String item in biasFilesList.Items)
            {
                string fitsFilename = "";
                fitsFilename = item.ToString();
                if (firstFilename == null)
                {
                    firstFilename = fitsFilename;
                }

                Fits f = new Fits(fitsFilename);
                int iHDU = 0;
                BasicHDU h;

                h = f.ReadHDU();
                if (h != null)
                {
                    if (iHDU == 0)
                    {
                        System.Console.Out.WriteLine("\n\nPrimary header:\n");
                    }
                    else
                    {
                        System.Console.Out.WriteLine("\n\nExtension " + iHDU + ":\n");
                    }
                    iHDU += 1;
                    h.Info();

                    // Object biasDataArray = h.Data.DataArray;

                    // h.Data.Kernel[0] = 1;



                    // short firstVal = biasDataArray[0][0];

                    // short temp_short = -31765;
                    // ushort temp_ushort = (ushort)temp_short;

                    //float[][] floatImg = (float[][])h.Kernel;

                    System.Array[] img_array = (System.Array[])h.Data.Kernel;
                    // ((System.Int16[])((System.Array[][])h.Data.Kernel)[0])[0] = 1;
                    // object pixel = ((System.Array)h.Data.Kernel);
                    // short[] short_array = ArrayFuncs.ConvertArray(img_array, short.Type);

                    // img_array.Cast<>
                    // System.Int16[] short_img_array = (System.Int16[])h.Data.Kernel;

                    // System.Array firstRow = img_array[0,0];
                    // short firstPixel = arrayInteger[0];

                    imageDimensions = ArrayFuncs.GetDimensions(img_array);
                    if (imageStack == null)
                    {
                        imageStack = new int[numberOfBiasImages, imageDimensions[0], imageDimensions[1]];
                    }
                    if (imageList == null)
                    {
                        imageList = new List<int[,]>();
                    }
                    int[,] imageCopy = new int[imageDimensions[0], imageDimensions[1]];
                    // int[,] img = (int[,])h.Data.Kernel;

                    // ArrayFuncs.CopyArray(img_array, imageCopy);

                    // System.Array row = img_array[0];
                    // int[][] image1 = img_array.Cast<int,int>().ToArray();
                    // short[,] short_image = new short[imageDimensions[0],imageDimensions[1]];

                    for (int i = 0; i < imageDimensions[0]; i++)
                    {
                        // int c = 1;

                        System.Array row = img_array[i];
                        short[] arrayInteger = img_array[i].Cast<short>().ToArray();

                        for (int j = 0; j < imageDimensions[1]; j++)
                        {
                            imageCopy[i, j] = (int)(arrayInteger[j] + 32768);
                            imageStack[imageCounter, i, j] = (int)(arrayInteger[j] + 32768);
                            // imageCopy[i, j] = (ushort)(arrayInteger[j]);
                            // imageStack[imageCounter, i, j] = (ushort)(arrayInteger[j]);
                        }

                        imageList.Add(imageCopy);

                        int c = 1;

                        // short[] image_row = (System.Array { short[] }) img_array[i];

                        // var result = img_array[i].Select(i => i + 32768);
                    }

                    // ushort[,] adjustedImage = (ushort[,])imageCopy
                }

                int a = 1;
                imageCounter += 1;
            }
            */
            // ComboBoxItem biasAveragingMethod = (ComboBoxItem)((ComboBox)sender).SelectedItem;
            int[,] stackedImage = new int[imageDimensions[0], imageDimensions[1]];
            

            
            string biasAveragingMethodString = averagingMethodBias.SelectionBoxItem.ToString();

            if (lowBiasSliderPanel != null)
            {
                switch (biasAveragingMethodString)
                {

                    case "Mean":
                        // int[,] imageMean = new int[imageDimensions[0], imageDimensions[1]];
                        // int[,] imageWinsorizedMean = new int[imageDimensions[0], imageDimensions[1]];
                        stackedImage = MeanOfStack(imageStack, imageDimensions);
                        // lowBiasSliderPanel.Visibility = Visibility.Hidden;
                        // highBiasSliderPanel.Visibility = Visibility.Hidden;
                        break;

                    case "Median":
                        // lowBiasSliderPanel.Visibility = Visibility.Hidden;
                        // highBiasSliderPanel.Visibility = Visibility.Hidden;
                        stackedImage = MedianOfStack(imageStack, imageDimensions);
                        break;

                    case "Winsorized Mean":
                        double lowLimit = lowLimitBias.Value;
                        double upperLimit = highLimitBias.Value;
                        int[,] imageMean = new int[imageDimensions[0], imageDimensions[1]];
                        // int[,] imageWinsorizedMean = new int[imageDimensions[0], imageDimensions[1]];
                        (stackedImage, imageMean) = WinsorizeStack(imageStack, imageDimensions, lowLimit, upperLimit);
                        // lowBiasSliderPanel.Visibility = Visibility.Visible;
                        // highBiasSliderPanel.Visibility = Visibility.Visible;
                        break;

                    default:
                        break;
                }
            }
            

            // (imageWinsorizedMean, imageMean) = WinsorizeStack(imageStack, imageDimensions);
            /*
            for (int i = 0; i < imageDimensions[0]; i++)
            {
                for (int j = 0; j < imageDimensions[1]; j++)
                {
                    int pixelSum = 0;

                    int[] arrayToBeWinsorized = new int[numberOfBiasImages];

                    for (int imageNumber = 0; imageNumber < numberOfBiasImages; imageNumber++)
                    {
                        int pixelValue = imageStack[imageNumber, i, j];
                        pixelSum += (int) pixelValue;
                        arrayToBeWinsorized[imageNumber] = pixelValue;
                    }

                    int pixelAverage = (int)((float)pixelSum / (float)numberOfBiasImages);

                    int d = 1;

                    imageMean[i, j] = pixelAverage;

                    // var sortFunction = new QuickSortMethods();
                    // var sortedArray = SortArray(arrayToBeWinsorized, 0, arrayToBeWinsorized.Length - 1);
                    int winsorizedMean = (int) WinsorizedMean(arrayToBeWinsorized, 0.1f, 0.2f);
                    // const short shortOffset = 32768;
                    imageWinsorizedMean[i, j] = (short)((int)winsorizedMean - (int)32768);
                    // imageWinsorizedMean[i, j] = (short)((short)winsorizedMean);
                }
            }
            */

            SaveFileDialog saveBiasFileDialog = new SaveFileDialog();
            saveBiasFileDialog.Filter = "FITS Files|*.fit;*.fits";
            saveBiasFileDialog.Title = "Please pick a file name for your Master Bias file...";
            // saveBiasFileDialog.Multiselect = false;
            saveBiasFileDialog.FileName = "Master Bias.fits";

            bool? biasSelectedSuccess = saveBiasFileDialog.ShowDialog();

            if (biasSelectedSuccess == true)
            {
                string saveFilename = saveBiasFileDialog.FileName;
                SaveFITSImage(firstFilename, saveFilename, stackedImage, imageDimensions);
            }
            /*
            Fits firstFITS = new Fits(firstFilename);
            int firstHDU = 0;
            BasicHDU firsth;

            firsth = firstFITS.ReadHDU();
            if (firsth != null)
            {
                System.Array[] img_array = (System.Array[])firsth.Data.Kernel;
                // short[][] short_array = (short[][])firsth.Data.Kernel;

                for (int i = 0; i < imageDimensions[0]; i++)
                {
                    short[] row = (short[]) img_array[i];
                    

                    short[] newRow = { 1,2,3 };
                    img_array[i] = newRow;
                    // short[] wRow = imageWinsorizedMean[i];
                    // ((System.Array[])firsth.Data.Kernel)[i] = newRow;

                    // System.Array img_array = (System.Array)firsth.Data.Kernel[i];

                    short[] currentRow = new short[imageDimensions[1]];

                    for (int j = 0; j < imageDimensions[1]; j++)
                    {
                        // row[j] = short i;
                        // img_array[i][j] = (short)(imageWinsorizedMean[i, j] - 32768);
                        // (short)((System.Array)((System.Array[])firsth.Data.Kernel)[i])[j] = (short)0;
                        currentRow[j] = (short)imageWinsorizedMean[i,j];
                        // currentRow[j] = (short) j;
                    }

                    ((System.Array[])firsth.Data.Kernel)[i] = currentRow;
                }

                // int asdf = 1;

                
                nom.tam.fits.Fits fits = new nom.tam.fits.Fits();
                nom.tam.fits.BasicHDU hdu = nom.tam.fits.Fits.MakeHDU(imageWinsorizedMean);
                hdu.AddValue("BITPIX", 16, null);  // set bit depth of 16 bit
                hdu.AddValue("NAXIS", 2, null);    // 2D-image
                fits.AddHDU(hdu);                  // Debugging here shows correct HDU-data, i.e. gradient from top left to bottom right
                nom.tam.util.BufferedFile file = new nom.tam.util.BufferedFile(@"C:\\Users\\Scott\\source\\repos\\BiasDarksFlats\\test.fits", System.IO.FileAccess.ReadWrite, System.IO.FileShare.ReadWrite);
                fits.Write(file);
                
                file.Flush();
                file.Close();
                

                nom.tam.util.BufferedFile file = new nom.tam.util.BufferedFile(@"C:\\Users\\Scott\\source\\repos\\BiasDarksFlats\\test.fits", System.IO.FileAccess.ReadWrite, System.IO.FileShare.ReadWrite);

                firstFITS.Write(file);
                file.Flush();
                file.Close();

                int asdf = 1;
            }
        */
        }

        private void clearFilesList_Click(object sender, RoutedEventArgs e)
        {
            biasFilesList.Items.Clear();

            loadBiasImages.Visibility = Visibility.Hidden;
            lowBiasSliderPanel.Visibility = Visibility.Hidden;
            highBiasSliderPanel.Visibility = Visibility.Hidden;
            averagingMethodBias.Visibility = Visibility.Hidden;
        }

        private void tabBiasDarksFlats_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        private void loadDarks_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog darksFileDialog = new OpenFileDialog();
            darksFileDialog.Filter = "Darks Files | *.fit;*.fits";
            darksFileDialog.Title = "Please pick all your Darks files...";
            darksFileDialog.Multiselect = true;

            bool? darksSelectedSuccess = darksFileDialog.ShowDialog();

            if (darksSelectedSuccess == true)
            {
                // darksFilesList
                string[] darksFilePaths = darksFileDialog.FileNames;

                foreach (string darksFile in darksFilePaths)
                {
                    darksFilesList.Items.Add(darksFile);
                }

                createMasterDark.Visibility = Visibility.Visible;
                lowDarksSliderPanel.Visibility = Visibility.Visible;
                highDarksSliderPanel.Visibility = Visibility.Visible;
                averagingMethodDarks.Visibility = Visibility.Visible;
            }
            else
            {
                // No files selected
                MessageBox.Show("No Bias Files Selected", "No File Selected", MessageBoxButton.OK, MessageBoxImage.Asterisk);
            }
        }

        private void clearDarksList_Click(object sender, RoutedEventArgs e)
        {
            darksFilesList.Items.Clear();

            createMasterDark.Visibility = Visibility.Hidden;
            lowDarksSliderPanel.Visibility = Visibility.Hidden;
            highDarksSliderPanel.Visibility = Visibility.Hidden;
            averagingMethodDarks.Visibility = Visibility.Hidden;
        }

        private void createMasterDark_Click(object sender, RoutedEventArgs e)
        {
            (int[,,] imageStack, int[] imageDimensions, string firstFilename) = LoadImageStack(darksFilesList.Items);

            int numberOfDarksImages = biasFilesList.Items.Count;

            int[,] stackedImage = new int[imageDimensions[0], imageDimensions[1]];

            string darksAveragingMethodString = averagingMethodDarks.SelectionBoxItem.ToString();

            if (lowBiasSliderPanel != null)
            {
                switch (darksAveragingMethodString)
                {

                    case "Mean":
                        stackedImage = MeanOfStack(imageStack, imageDimensions);
                        break;

                    case "Median":
                        stackedImage = MedianOfStack(imageStack, imageDimensions);
                        break;

                    case "Winsorized Mean":
                        double lowLimit = lowLimitDarks.Value;
                        double upperLimit = highLimitDarks.Value;
                        int[,] imageMean = new int[imageDimensions[0], imageDimensions[1]];
                        (stackedImage, imageMean) = WinsorizeStack(imageStack, imageDimensions, lowLimit, upperLimit);
                        break;

                    default:
                        break;
                }
            }


            SaveFileDialog saveDarksFileDialog = new SaveFileDialog();
            saveDarksFileDialog.Filter = "FITS Files|*.fit;*.fits";
            saveDarksFileDialog.Title = "Please pick a file name for your Master Darks file...";
            saveDarksFileDialog.FileName = "Master Dark.fits";

            bool? biasSelectedSuccess = saveDarksFileDialog.ShowDialog();

            if (biasSelectedSuccess == true)
            {
                string saveFilename = saveDarksFileDialog.FileName;
                SaveFITSImage(firstFilename, saveFilename, stackedImage, imageDimensions);
            }
        }

        private void loadFlats_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog flatsFileDialog = new OpenFileDialog();
            flatsFileDialog.Filter = "Flats Files |*.fit;*.fits";
            flatsFileDialog.Title = "Please pick all your Flats files...";
            flatsFileDialog.Multiselect = true;

            bool? biasSelectedSuccess = flatsFileDialog.ShowDialog();

            if (biasSelectedSuccess == true)
            {
                // biasFilesList
                string[] flatsFilePaths = flatsFileDialog.FileNames;

                foreach (string biasFile in flatsFilePaths)
                {
                    flatsFilesList.Items.Add(biasFile);
                }

                loadFlatsImages.Visibility = Visibility.Visible;
                lowFlatsSliderPanel.Visibility = Visibility.Visible;
                highFlatsSliderPanel.Visibility = Visibility.Visible;
                averagingMethodFlats.Visibility = Visibility.Visible;
                normalizationMethodFlats.Visibility = Visibility.Visible;
            }
            else
            {
                // No files selected
                MessageBox.Show("No Flats Files Selected", "No File Selected", MessageBoxButton.OK, MessageBoxImage.Asterisk);
            }
        }

        private void clearFlatsFilesList_Click(object sender, RoutedEventArgs e)
        {
            flatsFilesList.Items.Clear();

            loadFlatsImages.Visibility = Visibility.Hidden;
            lowFlatsSliderPanel.Visibility = Visibility.Hidden;
            highFlatsSliderPanel.Visibility = Visibility.Hidden;
            averagingMethodFlats.Visibility = Visibility.Hidden;
            normalizationMethodFlats.Visibility = Visibility.Hidden;
        }

        private void loadFlatsImages_Click(object sender, RoutedEventArgs e)
        {
            (int[,,] imageStack, int[] imageDimensions, string firstFilename) = LoadImageStack(flatsFilesList.Items);

            // double[] initialMedians = CalculateMediansInStack(imageStack);

            string flatsNormalizationMethodString = normalizationMethodFlats.SelectionBoxItem.ToString();

            switch (flatsNormalizationMethodString)
            {

                case "None":
                    break;

                case "Offset":
                    imageStack = NormalizeStackByAddition(imageStack);
                    break;

                case "Multiplicative":
                    imageStack = NormalizeStackByMultiplication(imageStack);
                    break;

                default:
                    break;
            }

            // double[] normalizedMedians = CalculateMediansInStack(imageStack);

            int numberOfFlatsImages = flatsFilesList.Items.Count;

            int[,] stackedImage = new int[imageDimensions[0], imageDimensions[1]];

            string flatsAveragingMethodString = averagingMethodFlats.SelectionBoxItem.ToString();

            if (lowBiasSliderPanel != null)
            {
                switch (flatsAveragingMethodString)
                {

                    case "Mean":
                        stackedImage = MeanOfStack(imageStack, imageDimensions);
                        break;

                    case "Median":
                        stackedImage = MedianOfStack(imageStack, imageDimensions);
                        break;

                    case "Winsorized Mean":
                        double lowLimit = lowLimitDarks.Value;
                        double upperLimit = highLimitDarks.Value;
                        int[,] imageMean = new int[imageDimensions[0], imageDimensions[1]];
                        (stackedImage, imageMean) = WinsorizeStack(imageStack, imageDimensions, lowLimit, upperLimit);
                        break;

                    default:
                        break;
                }
            }

            SaveFileDialog saveFlatsFileDialog = new SaveFileDialog();
            saveFlatsFileDialog.Filter = "FITS Files|*.fit;*.fits";
            saveFlatsFileDialog.Title = "Please pick a file name for your Master Flats file...";
            saveFlatsFileDialog.FileName = "Master Flat.fits";

            bool? biasSelectedSuccess = saveFlatsFileDialog.ShowDialog();

            if (biasSelectedSuccess == true)
            {
                string saveFilename = saveFlatsFileDialog.FileName;
                SaveFITSImage(firstFilename, saveFilename, stackedImage, imageDimensions);
            }
        }

        static private double MedianOf1DArray(int[] oneDArray)
        {
            int middle = oneDArray.Length / 2;
            if (oneDArray.Length % 2 == 1)
            {
                return (double)oneDArray[middle];
            }
            else
            {
                return ((double)oneDArray[middle - 1] + (double)oneDArray[middle]) / 2.0;
            }
        }

        static private double MedianOf2DArray(int[,] inputImage)
        {
            // Create a new list to store the items
            int[] sortedList = new int[inputImage.GetLength(0) * inputImage.GetLength(1)];
            // keep track of where we are.
            int listPos = 0;
            // iterate over the entire 2d array adding each integer
            for (int i = 0; i < inputImage.GetLength(0); i++)
            {
                for (int j = 0; j < inputImage.GetLength(1); j++)
                {
                    sortedList[listPos++] = inputImage[i, j];
                }
            }
            // sort the list.
            // Arrays.sort(list);
            int[] idx = new int[sortedList.Length];
            idx = SortArray(sortedList, 0, sortedList.Length - 1);
            return MedianOf1DArray(sortedList);
        }

        private void averagingMethodBias_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ComboBoxItem biasAveragingMethod = (ComboBoxItem)((ComboBox)sender).SelectedItem;

            if (biasAveragingMethod != null )
            {
                string biasAveragingMethodString = biasAveragingMethod.Content.ToString();

                if (lowBiasSliderPanel != null)
                {
                    switch (biasAveragingMethodString)
                    {

                        case "Mean":
                            lowBiasSliderPanel.Visibility = Visibility.Hidden;
                            highBiasSliderPanel.Visibility = Visibility.Hidden;
                            break;

                        case "Median":
                            lowBiasSliderPanel.Visibility = Visibility.Hidden;
                            highBiasSliderPanel.Visibility = Visibility.Hidden;
                            break;

                        case "Winsorized Mean":
                            lowBiasSliderPanel.Visibility = Visibility.Visible;
                            highBiasSliderPanel.Visibility = Visibility.Visible;
                            break;

                        default:
                            break;
                    }
                }
            }
        }

        private void averagingMethodDarks_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ComboBoxItem darksAveragingMethod = (ComboBoxItem)((ComboBox)sender).SelectedItem;

            if (darksAveragingMethod != null)
            {
                string darksAveragingMethodString = darksAveragingMethod.Content.ToString();

                if (lowDarksSliderPanel != null)
                {
                    switch (darksAveragingMethodString)
                    {

                        case "Mean":
                            lowDarksSliderPanel.Visibility = Visibility.Hidden;
                            highDarksSliderPanel.Visibility = Visibility.Hidden;
                            break;

                        case "Median":
                            lowDarksSliderPanel.Visibility = Visibility.Hidden;
                            highDarksSliderPanel.Visibility = Visibility.Hidden;
                            break;

                        case "Winsorized Mean":
                            lowDarksSliderPanel.Visibility = Visibility.Visible;
                            highDarksSliderPanel.Visibility = Visibility.Visible;
                            break;

                        default:
                            break;
                    }
                }
            }
        }

        static private double MedianOfSliceInArray(int[,,] imageStack, int sliceNumber)
        {
            // Create a new list to store the items
            int[] sortedList = new int[imageStack.GetLength(1) * imageStack.GetLength(2)];
            // keep track of where we are.
            int listPos = 0;
            // iterate over the entire 2d array adding each integer
            for (int i = 0; i < imageStack.GetLength(1); i++)
            {
                for (int j = 0; j < imageStack.GetLength(2); j++)
                {
                    sortedList[listPos++] = imageStack[sliceNumber, i, j];
                }
            }
            // sort the list.
            int[] idx = new int[sortedList.Length];
            idx = SortArray(sortedList, 0, sortedList.Length - 1);
            return MedianOf1DArray(sortedList);
        }

        private double[] CalculateMediansInStack(int[,,] imageStack)
        {
            double[] medianList = new double[imageStack.GetLength(0)];
            for (int i = 0; i < imageStack.GetLength(0); i++)
            {
                medianList[i] = MedianOfSliceInArray(imageStack, i);
            }

            return medianList;
        }

        private int[,,] NormalizeStackByMultiplication(int[,,] imageStack)
        {
            double[] medianList = CalculateMediansInStack(imageStack);

            for (int sliceNumber = 1; sliceNumber < imageStack.GetLength(0); sliceNumber++)
            {
                double sliceCorrectionFactor = medianList[0] / medianList[sliceNumber];

                for (int i = 0;i < imageStack.GetLength(1);i++)
                {
                    for (int j = 0;j < imageStack.GetLength(2);j++)
                    {
                        imageStack[sliceNumber,i,j] = (int) ((double) imageStack[sliceNumber,i,j] * sliceCorrectionFactor);
                    }
                }
            }

            return imageStack;
        }

        private int[,,] NormalizeStackByAddition(int[,,] imageStack)
        {
            double[] medianList = CalculateMediansInStack(imageStack);

            for (int sliceNumber = 1; sliceNumber < imageStack.GetLength(0); sliceNumber++)
            {
                double sliceCorrectionFactor = medianList[0] - medianList[sliceNumber];

                for (int i = 0; i < imageStack.GetLength(1); i++)
                {
                    for (int j = 0; j < imageStack.GetLength(2); j++)
                    {
                        imageStack[sliceNumber, i, j] = (int)((double)imageStack[sliceNumber, i, j] + sliceCorrectionFactor);
                    }
                }
            }

            return imageStack;
        }

        private void averagingMethodFlats_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ComboBoxItem flatsAveragingMethod = (ComboBoxItem)((ComboBox)sender).SelectedItem;

            if (flatsAveragingMethod != null)
            {
                string flatsAveragingMethodString = flatsAveragingMethod.Content.ToString();

                if (lowFlatsSliderPanel != null)
                {
                    switch (flatsAveragingMethodString)
                    {

                        case "Mean":
                            lowFlatsSliderPanel.Visibility = Visibility.Hidden;
                            highFlatsSliderPanel.Visibility = Visibility.Hidden;
                            break;

                        case "Median":
                            lowFlatsSliderPanel.Visibility = Visibility.Hidden;
                            highFlatsSliderPanel.Visibility = Visibility.Hidden;
                            break;

                        case "Winsorized Mean":
                            lowFlatsSliderPanel.Visibility = Visibility.Visible;
                            highFlatsSliderPanel.Visibility = Visibility.Visible;
                            break;

                        default:
                            break;
                    }
                }
            }
        }

        private void loadBiasForSuperbias_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog superbiasFileDialog = new OpenFileDialog();
            superbiasFileDialog.Filter = "Master Bias File |*.fit;*.fits";
            superbiasFileDialog.Title = "Please pick your Master Bias file...";
            superbiasFileDialog.Multiselect = false;

            bool? biasSelectedSuccess = superbiasFileDialog.ShowDialog();

            if (biasSelectedSuccess == true)
            {
                // biasFilesList
                string[] biasFilePaths = superbiasFileDialog.FileNames;

                string biasFilename = biasFilePaths[0];

                int a = 1;

                Fits f = new Fits(biasFilename);
                // int iHDU = 0;
                BasicHDU h;

                h = f.ReadHDU();
                if (h != null)
                {
                    /*
                    if (iHDU == 0)
                    {
                        System.Console.Out.WriteLine("\n\nPrimary header:\n");
                    }
                    else
                    {
                        System.Console.Out.WriteLine("\n\nExtension " + iHDU + ":\n");
                    }
                    // iHDU += 1;
                    // h.Info();
                    */
                    System.Array[] img_array = (System.Array[])h.Data.Kernel;

                    int[] imageDimensions = ArrayFuncs.GetDimensions(img_array);
                    /*
                    if (imageStack == null)
                    {
                        imageStack = new int[numberOfBiasImages, imageDimensions[0], imageDimensions[1]];
                    }
                    if (imageList == null)
                    {
                        imageList = new List<int[,]>();
                    }
                    */
                    int[,] imageCopy = new int[imageDimensions[0], imageDimensions[1]];

                    for (int i = 0; i < imageDimensions[0]; i++)
                    {
                        System.Array row = img_array[i];
                        short[] arrayInteger = img_array[i].Cast<short>().ToArray();

                        for (int j = 0; j < imageDimensions[1]; j++)
                        {
                            imageCopy[i, j] = (int)(arrayInteger[j] + 32768);
                            // imageStack[imageCounter, i, j] = (int)(arrayInteger[j] + 32768);
                        }
                    }

                    int[,] superbiasImage = new int[imageDimensions[0], imageDimensions[1]];

                    int medianFilterSize = (int) superbiasKernelSize.Value;
                    int filterWidth = (int)(((double)medianFilterSize / 2.0) - 0.1);

                    for (int i = 0; i < imageDimensions[0]; i++)
                    {
                        for (int j = 0; j < imageDimensions[1]; j++)
                        {
                            int iLowerExtent = i - filterWidth;

                            if (iLowerExtent < 0)
                            {
                                iLowerExtent = 0;
                            }

                            int iUpperExtent = i + filterWidth;

                            if (iUpperExtent > imageDimensions[0] - 1)
                            {
                                iUpperExtent = imageDimensions[0] - 1;
                            }

                            int[] medianStrip = new int[iUpperExtent - iLowerExtent + 1];
                            int medianStripCounter = 0;

                            for (int medianStripPosition = iLowerExtent; medianStripPosition < iUpperExtent+1; medianStripPosition++)
                            {
                                medianStrip[medianStripCounter] = imageCopy[medianStripPosition, j];
                                medianStripCounter++;
                            }

                            medianStrip = SortArray(medianStrip, 0, medianStrip.Length - 1);

                            int medianOfPosition = (int) MedianOf1DArray(medianStrip);

                            superbiasImage[i,j] = medianOfPosition - 32768;
                        }
                    }

                    SaveFileDialog saveSuperbiasFileDialog = new SaveFileDialog();
                    saveSuperbiasFileDialog.Filter = "FITS Files|*.fit;*.fits";
                    saveSuperbiasFileDialog.Title = "Please pick a file name for your Master Flats file...";
                    saveSuperbiasFileDialog.FileName = "Master Flat.fits";

                    bool? superbiasSelectedSuccess = saveSuperbiasFileDialog.ShowDialog();

                    if (biasSelectedSuccess == true)
                    {
                        string saveFilename = saveSuperbiasFileDialog.FileName;
                        SaveFITSImage(biasFilename, saveFilename, superbiasImage, imageDimensions);
                    }
                }
                else
                {
                    // No files selected
                    MessageBox.Show("No Bias File Selected For Superbias", "No File Selected", MessageBoxButton.OK, MessageBoxImage.Asterisk);
                }
            }
        }

        private void loadDarkForSuperdark_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog superdarkFileDialog = new OpenFileDialog();
            superdarkFileDialog.Filter = "Master Dark File |*.fit;*.fits";
            superdarkFileDialog.Title = "Please pick your Master Dark file...";
            superdarkFileDialog.Multiselect = false;

            bool? darkSelectedSuccess = superdarkFileDialog.ShowDialog();

            if (darkSelectedSuccess == true)
            {
                // biasFilesList
                string[] biasFilePaths = superdarkFileDialog.FileNames;

                string darkFilename = biasFilePaths[0];

                Fits f = new Fits(darkFilename);
                
                BasicHDU h;

                h = f.ReadHDU();
                if (h != null)
                {
                    System.Array[] img_array = (System.Array[])h.Data.Kernel;

                    int[] imageDimensions = ArrayFuncs.GetDimensions(img_array);
                    
                    int[,] imageCopy = new int[imageDimensions[0], imageDimensions[1]];

                    for (int i = 0; i < imageDimensions[0]; i++)
                    {
                        System.Array row = img_array[i];
                        short[] arrayInteger = img_array[i].Cast<short>().ToArray();

                        for (int j = 0; j < imageDimensions[1]; j++)
                        {
                            imageCopy[i, j] = (int)(arrayInteger[j] + 32768);
                        }
                    }

                    int[,] superdarkImage = new int[imageDimensions[0], imageDimensions[1]];

                    int medianFilterSize = (int)superdarkKernelSize.Value;
                    int filterWidth = (int)(((double)medianFilterSize / 2.0) - 0.1);

                    for (int i = 0; i < imageDimensions[0]; i++)
                    {
                        for (int j = 0; j < imageDimensions[1]; j++)
                        {
                            int iLowerExtent = i - filterWidth;

                            if (iLowerExtent < 0)
                            {
                                iLowerExtent = 0;
                            }

                            int iUpperExtent = i + filterWidth;

                            if (iUpperExtent > imageDimensions[0] - 1)
                            {
                                iUpperExtent = imageDimensions[0] - 1;
                            }

                            int[] medianStrip = new int[iUpperExtent - iLowerExtent + 1];
                            int medianStripCounter = 0;

                            for (int medianStripPosition = iLowerExtent; medianStripPosition < iUpperExtent + 1; medianStripPosition++)
                            {
                                medianStrip[medianStripCounter] = imageCopy[medianStripPosition, j];
                                medianStripCounter++;
                            }

                            medianStrip = SortArray(medianStrip, 0, medianStrip.Length - 1);

                            int medianOfPosition = (int)MedianOf1DArray(medianStrip);

                            superdarkImage[i, j] = medianOfPosition;
                        }
                    }

                    double upperBoundLimit = 1.0 + ((double)superdarkCutoff.Value / 100.0);
                    double lowerBoundLimit = 1.0 - ((double)superdarkCutoff.Value / 100.0);

                    for (int i = 0; i < imageDimensions[0]; i++)
                    {
                        for (int j = 0; j < imageDimensions[1]; j++)
                        {
                            double deviationRatio = ((double) imageCopy[i, j] / (double) superdarkImage[i, j]);

                            if ((deviationRatio > upperBoundLimit) || (deviationRatio < lowerBoundLimit))
                            {
                                superdarkImage[i, j] = imageCopy[i, j];
                            }

                            superdarkImage[i, j] = superdarkImage[i, j] - 32768;
                        }
                    }

                    SaveFileDialog saveSuperdarkFileDialog = new SaveFileDialog();
                    saveSuperdarkFileDialog.Filter = "FITS Files|*.fit;*.fits";
                    saveSuperdarkFileDialog.Title = "Please pick a file name for your Superdark file...";
                    saveSuperdarkFileDialog.FileName = "Superdark " + superdarkCutoff.Value.ToString() + ".fits";

                    bool? superbiasSelectedSuccess = saveSuperdarkFileDialog.ShowDialog();

                    if (darkSelectedSuccess == true)
                    {
                        string saveFilename = saveSuperdarkFileDialog.FileName;
                        SaveFITSImage(darkFilename, saveFilename, superdarkImage, imageDimensions);
                    }
                }
                else
                {
                    // No files selected
                    MessageBox.Show("No Dark File Selected For Superdark", "No File Selected", MessageBoxButton.OK, MessageBoxImage.Asterisk);
                }
            }
        }
    }
}