using ImageScan.Helper;
using ImageScan.Utils;
using Imaging.Library;
using Imaging.Library.Entities;
using Imaging.Library.Enums;
using Imaging.Library.Filters.ComplexFilters;
using Imaging.Library.Maths;
using Plugin.Media;
using Plugin.Media.Abstractions;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using Xamarin.Essentials;
using Xamarin.Forms;
using static Xamarin.Essentials.Permissions;
using Point = Imaging.Library.Entities.Point;

namespace ImageScan
{
    // Learn more about making custom code visible in the Xamarin.Forms previewer
    // by visiting https://aka.ms/xamarinforms-previewer
    [DesignTimeVisible(false)]
    public partial class MainPage : ContentPage
    {
        public MainPage()
        {
            InitializeComponent();
        }
        private async void Button_Clicked(object sender, EventArgs e)
        {
            box.IsVisible = true;
            stackloading.IsVisible = true;
            loading.IsVisible = true;
            loading.IsRunning = true;
            var status = await CheckAndRequestPermissionAsync(new Permissions.StorageRead());
            if (status != PermissionStatus.Granted)
            {
                // Notify user permission was denied
                return;
            }
            var status1 = await CheckAndRequestPermissionAsync(new Permissions.StorageWrite());
            if (status1 != PermissionStatus.Granted)
            {
                // Notify user permission was denied
                return;
            }
            await CrossMedia.Current.Initialize();
            var file = await CrossMedia.Current.PickPhotoAsync(new PickMediaOptions {
                CompressionQuality = 75,
                PhotoSize = PhotoSize.Large
            });
            if (file != null)
            {
                switch (Device.RuntimePlatform)
                {
                    case Device.Android:
                        Source = AndroidHelper.GetPixelMap(file.GetStream());
                        break;

                    case Device.iOS:
                        Source = iOSHelper.GetPixelMap(file.Path);
                        break;
                }
                convert();
            }
            else
            {
                box.IsVisible = false;
                loading.IsVisible = false;
                loading.IsRunning = false;
                stackloading.IsVisible = false;

            }
        }
        public PixelMap Source { get; set; }
        public void convert()
        {

            var imaging = new ImagingManager(Source);

            var scale = 0.4;

            imaging.AddFilter(new Imaging.Library.Filters.BasicFilters.BicubicFilter(scale)); //Downscaling
            imaging.Render();

            imaging.AddFilter(new CannyEdgeDetector());
            imaging.Render();

            var blobCounter = new BlobCounter
            {
                ObjectsOrder = ObjectsOrder.Size
            };
            imaging.AddFilter(blobCounter);

            imaging.Render();

            List<Point> corners = null;
            var blobs = blobCounter.GetObjectsInformation();
            foreach (var blob in blobs)
            {
                var points = blobCounter.GetBlobsEdgePoints(blob);

                var shapeChecker = new SimpleShapeChecker();

                if (shapeChecker.IsQuadrilateral(points, out corners))
                    break;
            }

            var edgePoints = new EdgePoints();
            edgePoints.SetPoints(corners.ToArray());

            imaging.Render();
            imaging.UndoAll();

            edgePoints = edgePoints.ZoomIn(scale);
            imaging.AddFilter(new QuadrilateralTransformation(edgePoints, true));

            imaging.Render();

            var strm = StreamLoadFromPixel(imaging.Output);

            var memoryStream = new MemoryStream();
            strm.CopyTo(memoryStream);
            DependencyService.Get<ISaveViewFile>().SaveAndViewAsync("out.jpg", memoryStream);
            myimg.Source = LoadFromPixel(imaging.Output);
            box.IsVisible = false;
            loading.IsVisible = false;
            loading.IsRunning = false;
            stackloading.IsVisible = false;
        }
        private ImageSource LoadFromPixel(PixelMap pixelMap)
        {
            switch (Device.RuntimePlatform)
            {
                case Device.Android:
                    return AndroidHelper.LoadImageFromPixelMap(pixelMap);

                case Device.iOS:
                    return iOSHelper.LoadImageFromPixelMap(pixelMap);

                default:
                    return null;
            }
        }
        private Stream StreamLoadFromPixel(PixelMap pixelMap)
        {
            switch (Device.RuntimePlatform)
            {
                case Device.Android:
                    return AndroidHelper.StreamFromPixelMap(pixelMap);

                //case Device.iOS:
                //    return iOSHelper.LoadImageFromPixelMap(pixelMap);

                default:
                    return null;
            }
        }
        public async Task<PermissionStatus> CheckAndRequestPermissionAsync<T>(T permission)
            where T : BasePermission
        {
            var status = await permission.CheckStatusAsync();
            if (status != PermissionStatus.Granted)
            {
                status = await permission.RequestAsync();
            }

            return status;
        }
    }
}
