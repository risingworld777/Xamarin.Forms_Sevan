﻿using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Acr.UserDialogs;
using PropertyChanged;
using Xamarin.Forms;
using XOCV.Interfaces;
using XOCV.Models;
using XOCV.Models.ResponseModels;
using XOCV.PageModels.Base;
using XOCV.Enums;
using Newtonsoft.Json;
using System.Diagnostics;
using XOCV.PageModels.Popups;

namespace XOCV.PageModels
{
	[ImplementPropertyChanged]
	public class PollPageModel : BasePageModel
	{
        #region Fields
        private ImageSource _picture;
		private int _currentCapturePosition;
        ObservableCollection<CarouselPageModel> _carouselPages;
        #endregion

        #region Properties
        public bool IsLastCapture { get; set; }
		public bool IsFirstCapture { get; set; }
		public long LastCaptureId { get; set; }
		public string CurrentFormName => ListOfCaptures[CurrentCapturePosition].Title;
		public FormModel BuildModel { get; set; }
		public List<FormModel> ListOfCaptures { get; set; }
		public ComplexFormsModel ComplexForms { get; set; }
		public ObservableCollection<NavigationModel> FormNames { get; set; }
		public ObservableCollection<ObservableCollection<ComplexItemPhotosModel>> PollPictures { get; set; }
		public ObservableCollection<string> CurrentImageSources { get; set; }
		public string Status { get; set; }
		//public DBModel DbModel { get; set; }
		public int DbModelId { get; set; }
		public long FormID { get; set; }
		public string StoreNumber { get; set; }
		public bool needsToUpdatePhotos { get; set; }
		public int[] CurrentCarouselPosition { get; set; }
        public int CurrentTimeOnTheForm { get; set; }
        public bool IsTimerWorking { get; set; }
		//public ObservableCollection<CarouselPageModel> CarouselPages
		//{
		//	get
		//	{
		//		return _carouselPages;
		//	}
		//	set
		//	{
		//		_carouselPages = value;
		//		CurrentPage = CarouselPages.FirstOrDefault();
		//	}
		//}
		//public CarouselPageModel CurrentPage { get; set; }
		public int CurrentCapturePosition
		{
			get { return _currentCapturePosition; }
			set
			{
				UserDialog.ShowLoading();

				var DbModel = new DBModel { ID = DbModelId, FormID = FormID, StoreNumber = StoreNumber };
				ComplexForms.Captures.First().Date = DateTime.UtcNow;
				DbModel.Content = JsonConvert.SerializeObject(ComplexForms);
				DbModel.Date = ComplexForms.Captures.FirstOrDefault().Date;
				new Task(() => App.DataBase.SaveContent(DbModel)).Start();

				if (IsLastCapture && _currentCapturePosition < value)
				{
					CoreMethods.PushPageModel<FormDetailsPageModel>(App.DataBase.GetContent().FirstOrDefault());
				}
				else
				{
					IsFirstCapture = false;
					IsLastCapture = false;
					if (value == 0)
					{
						IsFirstCapture = true;
					}
					else if (value == ListOfCaptures.Count - 1)
					{
						IsLastCapture = true;
					}
					if (ListOfCaptures[value] != null)
					{
						BuildModel = ListOfCaptures[value];
						_currentCapturePosition = value;
						PollPictures.Clear();
						for (int i = 0; i < BuildModel.MultiComplexItems.Count; i++)
						{
							var listOfImageSources = new ObservableCollection<ComplexItemPhotosModel>();
							for (int j = 0; j < BuildModel.MultiComplexItems[i].ComplexItems.Count; j++)
							{
								var count = BuildModel.MultiComplexItems[i].ComplexItems[j].Items.Where(item => item.Name == "imageKey").ToList().Count();
								if (count != 0)
								{
									for (int n = 0; n < count; n++)
									{
										BuildModel.MultiComplexItems[i].ComplexItems[j].ItemImageModels.Add(new ObservableCollection<string>());
									}
								}
								var ciom = new ComplexItemPhotosModel();
								if (BuildModel.MultiComplexItems[i].ComplexItems[j].ItemImageModels.Count != 0)
								{
									var imageCollections = new List<int>();
									if (BuildModel.MultiComplexItems[i].ComplexItems[j].Items.Where(it => it.Name == "imageKey").Count() != 0)
									{
										foreach (var collection in BuildModel.MultiComplexItems[i].ComplexItems[j].Items.Where(it => it.Name == "imageKey"))
										{
											imageCollections.Add(collection.Images.Count);
										}
									}
									ciom.Pictures = imageCollections;
								}
								listOfImageSources.Add(ciom);
							}
							PollPictures.Add(listOfImageSources);
						}
					}
				}
				UserDialog.HideLoading();

			    IsTimerWorking = true;
                BackgroundTimer(IsTimerWorking);
            }
		}
		#endregion

		#region Services
		public IWebApiHelper WebApiHelper { get; set; }
		public ICameraProvider CameraProvider { get; set; }
		public IPictureService PictureService { get; set; }
		public IUserDialogs UserDialog { get; set; }
		#endregion

		#region Commands
		public ICommand SavePollResultCommand => new Command(async () => await SavePollResultCommandExecute());
		public ICommand SaveAllPicturesToGalleryCommand => new Command(async () => await SaveAllPicturesToGalleryCommandExecute());
		public ICommand ExitCommand => new Command(async () => await ExitCommandExecute());
		public ICommand GoToNextCaptureCommand => new Command(GoToNextCaptureCommandExecute);
		public ICommand GoToPrevCaptureCommand => new Command(GoToPrevCaptureCommandExecute);
		public ICommand GoToCaptureCommand => new Command<int>(async (index) => await GoToCaptureCommandExecute(index));
		public ICommand TakePictureCommand => new Command<int []>(async (args) => await TakePictureCommandExecute(args[0], args[1], args [2]));
		#endregion

		#region Constructors
		public PollPageModel() {}
		public PollPageModel(IWebApiHelper webApiHelper, IUserDialogs userDialog, ICameraProvider cameraProvider, IPictureService pictureService)
		{
			WebApiHelper = webApiHelper;
			UserDialog = userDialog;
			CameraProvider = cameraProvider;
			PictureService = pictureService;
			IsFirstCapture = true;
		}
		#endregion

		#region Initialize
		public override void Init(object initData)
		{
			CurrentCarouselPosition = new int[3];
			CurrentImageSources = new ObservableCollection<string>();
			var DbModel = initData as DBModel;
			DbModelId = DbModel.ID;
			FormID = DbModel.FormID;
			StoreNumber = DbModel.StoreNumber;
			if (DbModel != null)
			{
				ComplexForms = JsonConvert.DeserializeObject<ComplexFormsModel>(DbModel.Content);
			}
			BuildModel = ComplexForms.Forms.FirstOrDefault();
			if (ComplexForms != null)
			{
				ListOfCaptures = ComplexForms.Forms;

				LastCaptureId = ListOfCaptures.LastOrDefault().Id;
				CurrentCapturePosition = 0;
				BuildModel = ListOfCaptures.FirstOrDefault();
				IsLastCapture = false;
			}
			PollPictures = new ObservableCollection<ObservableCollection<ComplexItemPhotosModel>>();
			for (int i = 0; i < BuildModel.MultiComplexItems.Count; i++)
			{
				var listOfImageSources = new ObservableCollection<ComplexItemPhotosModel>();
				for (int j = 0; j < BuildModel.MultiComplexItems[i].ComplexItems.Count; j++)
				{
					var count = BuildModel.MultiComplexItems[i].ComplexItems[j].Items.Where(item => item.Name == "imageKey").ToList().Count();
					if (count != 0)
					{
						for (int n = 0; n < count; n++)
						{
							BuildModel.MultiComplexItems[i].ComplexItems[j].ItemImageModels.Add(new ObservableCollection<string>());
						}
					}
					var ciom = new ComplexItemPhotosModel();
					if (BuildModel.MultiComplexItems[i].ComplexItems[j].ItemImageModels.Count != 0)
					{
						var imageCollections = new List<int>();
						if (BuildModel.MultiComplexItems[i].ComplexItems[j].Items.Where(it => it.Name == "imageKey").Count() != 0)
						{
							foreach (var collection in BuildModel.MultiComplexItems[i].ComplexItems[j].Items.Where(it => it.Name == "imageKey"))
							{
								imageCollections.Add(collection.Images.Count);
							}
						}
						ciom.Pictures = imageCollections;
					}
					listOfImageSources.Add(ciom);
				}
				PollPictures.Add(listOfImageSources);
			}
			FormNames = new ObservableCollection<NavigationModel>();
			foreach (var form in ListOfCaptures)
			{
				string name = null;
				if (form.Title == "Elevation A Bldg Signs")
				{
					name = "El A Bldg Signs";
				}
				else if (form.Title == "Elevation B Bldg Signs")
				{
					name = "El B Bldg Signs";
				}
				else if (form.Title == "Elevation C Bldg Signs")
				{
					name = "El C Bldg Signs";
				}
				else if (form.Title == "Elevation D Bldg Signs")
				{
					name = "El D Bldg Signs";
				}
				else
				{
					name = form.Title;
				}
				FormNames.Add(new NavigationModel
				{
					FormName = name,
				});
			}
			MessagingCenter.Subscribe<PhotoSignaturePageModel>(this, "OnPhotoEditDone", (obj) => { RefreshPhotoSections(); });

		    IsTimerWorking = true;
            BackgroundTimer(IsTimerWorking);
		}
		#endregion

		#region Command execution
		private async Task ExitCommandExecute()
		{
			var DbModel = new DBModel { ID = DbModelId, FormID = FormID, StoreNumber = StoreNumber };
			try
			{
				foreach (var capture in ComplexForms.Captures)
				{
					capture.SyncStatus = SyncStatus.NotSync;
					capture.FormStatus = FormStatus.Incomplete;
				}
				DbModel.Content = JsonConvert.SerializeObject(ComplexForms);
				DbModel.FormStatus = FormStatus.Incomplete;
				DbModel.SyncStatus = SyncStatus.NotSync;
				DbModel.Date = ComplexForms.Captures.FirstOrDefault().Date;
				new Task(() => App.DataBase.SaveContent(DbModel)).Start();
				await CoreMethods.PushPageModel<FormDetailsPageModel>(FormDetailsPageModel.initDataToReturn);
			}
			catch (Exception ex)
			{
				Debug.WriteLine(ex.Message);
				await UserDialog.AlertAsync("Saving to db failed!", "Warning!", "Ok");
			}
		}
		private async Task SavePollResultCommandExecute()
		{
			try
			{
				var DbModel = new DBModel { ID = DbModelId, FormID = FormID, StoreNumber = StoreNumber };
				foreach (var capture in ComplexForms.Captures)
				{
					capture.SyncStatus = SyncStatus.NotSync;
					capture.FormStatus = FormStatus.Complete;
				}
				DbModel.Content = JsonConvert.SerializeObject(ComplexForms);
				DbModel.FormStatus = FormStatus.Complete;
				DbModel.SyncStatus = SyncStatus.NotSync;
				DbModel.Date = ComplexForms.Captures.FirstOrDefault().Date;
				new Task(() => App.DataBase.SaveContent(DbModel)).Start();
				await CoreMethods.PushPageModel<FormDetailsPageModel>(FormDetailsPageModel.initDataToReturn);
			}
			catch (Exception ex)
			{
				Debug.WriteLine(ex.Message);
				await UserDialog.AlertAsync("Saving to db failed!", "Warning!", "Ok");
			}
		}
		private void GoToNextCaptureCommandExecute()
		{
			CurrentCapturePosition++;
		}
		private void GoToPrevCaptureCommandExecute()
		{
			CurrentCapturePosition--;
		}
		private async Task GoToCaptureCommandExecute(int index)
		{
			UserDialog.ShowLoading();

		    IsTimerWorking = false;
		    BackgroundTimer(IsTimerWorking);
		    BuildModel.AmountOfTimeSpentOnCurrentPageOfTheFormInSeconds = CurrentTimeOnTheForm;
            CurrentTimeOnTheForm = 0;

            CurrentCapturePosition = index;
			UserDialog.HideLoading();
		}

		public async Task SaveAllPicturesToGalleryCommandExecute()
		{
			UserDialog.ShowLoading("Preparing to save...");
			int countOfIamges = 0;
			foreach (var page in ListOfCaptures)
			{
				foreach (var multiComplexItem in page.MultiComplexItems)
				{
					foreach (var complexItem in multiComplexItem.ComplexItems)
					{
						foreach (var item in complexItem.Items.Where(i => i.Name == "imageKey"))
						{
							foreach (var image in item.Images)
							{
								countOfIamges++;
							}
						}
					}
				}
			}
			int currentImage = 0;
			foreach (var page in ListOfCaptures)
			{
				foreach (var multiComplexItem in page.MultiComplexItems)
				{
					foreach (var complexItem in multiComplexItem.ComplexItems)
					{
						foreach (var item in complexItem.Items.Where(i => i.Name == "imageKey"))
						{
							foreach (var image in item.Images)
							{
								UserDialog.ShowLoading(string.Format("Saving to galery {0} of {1}...", currentImage++, countOfIamges));
								PictureService.SavePictureToGallery(PictureService.GetPhotoPath(image));
								await Task.Delay(200);
							}
						}
					}
				}
			}
			UserDialog.HideLoading();
		}


		#endregion

		#region Additional methods
		public void SetCarouselItems(int id, int subId, int carouselId)
		{
			var images = BuildModel.MultiComplexItems[id].ComplexItems[subId].Items.Where(i => i.Name == "imageKey").ToList()[carouselId].Images;
			var itemImageModels = BuildModel.MultiComplexItems[id].ComplexItems[subId].ItemImageModels[carouselId];
			string [] path = { BuildModel.StoreNumber, 
				BuildModel.MultiComplexItems[id].ComplexItems[0].ComplexItemID.ToString(), 
				BuildModel.MultiComplexItems[id].ComplexItems[0].Items.Where(i => i.Name == "imageKey").ToList()[carouselId].ItemId.ToString()};
			object[] args = { images, itemImageModels ,path };
			CoreMethods.PushPageModel<GalleryPageModel>(args);
		}

		void RefreshPhotoSections()
		{
			ObservableCollection<ObservableCollection<ComplexItemPhotosModel>> pic = new ObservableCollection<ObservableCollection<ComplexItemPhotosModel>>();

			foreach (var p in PollPictures)
			{
				pic.Add(p);
			}

			PollPictures.Clear();
			PollPictures = pic;
		}

		protected override void ViewIsAppearing(object sender, EventArgs e)
		{
			base.ViewIsAppearing(sender, e);
			UpdatePhotos();
		}

	    private void BackgroundTimer(bool isTimerWorking = true)
	    {
            Device.StartTimer(TimeSpan.FromSeconds(1), () => 
            {
                CurrentTimeOnTheForm++;
                return isTimerWorking;
            });
        }
        #endregion

		private async Task<MediaFile> TakePictureCommandExecute(int id, int subId, int carouselId)
		{
			try
			{
				return await CameraProvider.TakePhotoAsync(new Helpers.CameraMediaStorageOptions
				{
					DefaultCamera = Helpers.CameraDevice.Rear,
					MaxPixelDimension = 400
				}).ContinueWith(
					t =>
					{
					    try
					    {
                            if (t.IsFaulted)
                            {
                                Status = t.Exception.InnerException.ToString();
                                UserDialog.AlertAsync("Taking photo failed!", "Warning!", "Ok");
                            }
                            else if (t.IsCanceled)
                            {
                                Status = "Canceled";
                            }
                            else
                            {
                                var mediaFile = t.Result;
                                SavePhoto(mediaFile, id, subId, carouselId).Wait();
                                GC.Collect();
                                return mediaFile;
                            }
                        }
					    catch (Exception ex)
					    {
                            Debug.WriteLine(ex.Message);
                            UserDialog.Alert("Taking photo failed!", "Warning!", "Ok");
                            return null;
                        }

						return null;
					});
			}
			catch (Exception ex)
			{
				Debug.WriteLine(ex.Message);
				await UserDialog.AlertAsync("Taking photo failed!", "Warning!", "Ok");
				return null;
			}
		}

		public async Task SavePhoto(MediaFile mediaFile, int id, int subId, int carouselId)
		{
			try
			{
				string fileName = string.Format("{0}_{1}_{2}_{3}",
					BuildModel.StoreNumber,
					BuildModel.MultiComplexItems[id].ComplexItems[0].ComplexItemID.ToString(),
					BuildModel.MultiComplexItems[id].ComplexItems[0].Items.Where(i => i.Name == "imageKey").ToList()[carouselId].ItemId.ToString(),
					DateTime.Now.ToString("yy-MM-dd_hh-mm-ss"));

				await PictureService.SavePictureToDisk(ImageSource.FromStream(() => mediaFile.Source), fileName);

				var image = PictureService.GetPictureFromDisk(fileName);

				BuildModel.MultiComplexItems[id].ComplexItems[subId].Items.Where(it => it.Name == "imageKey").ToList()[carouselId].Images.Add(fileName + ".jpg");
				BuildModel.MultiComplexItems[id].ComplexItems[subId].ItemImageModels[carouselId].Add(image);
				UpdatePhotos();
			}
			catch (Exception ex)
			{
				Debug.WriteLine(ex.Message);
				await UserDialog.AlertAsync("Saving photo failed!", "Warning!", "Ok");
			}
		}

        // ToDo: need to check this method (UpdatePhotos()) and fix adding images to list!
        private void UpdatePhotos()
		{
		    try
		    {
                PollPictures = null;
                PollPictures = new ObservableCollection<ObservableCollection<ComplexItemPhotosModel>>();
                for (int i = 0; i < BuildModel.MultiComplexItems.Count; i++)
                {
                    var listOfImageSources = new ObservableCollection<ComplexItemPhotosModel>();
                    for (int j = 0; j < BuildModel.MultiComplexItems[i].ComplexItems.Count; j++)
                    {

                        var ciom = new ComplexItemPhotosModel();
                        if (BuildModel.MultiComplexItems[i].ComplexItems[j].ItemImageModels.Count != 0)
                        {
                            var imageCollections = new List<int>();
                            if (BuildModel.MultiComplexItems[i].ComplexItems[j].Items.Where(it => it.Name == "imageKey").Count() != 0)
                            {
                                foreach (var collection in BuildModel.MultiComplexItems[i].ComplexItems[j].Items.Where(it => it.Name == "imageKey"))
                                {
                                    imageCollections.Add(collection.Images.Count);
                                }
                            }
                            ciom.Pictures = imageCollections;
                        }
                        listOfImageSources.Add(ciom);
                    }
                    PollPictures.Add(listOfImageSources);
                }
                MessagingCenter.Send<PollPageModel>(this, "onImageInteracted");
            }
            catch (Exception e)
		    {
                string message = e.Message;
                Debug.WriteLine("Error: {0}", message);

                #if DEBUG
                UserDialog.AlertAsync(message, "Error!", "OK");
                #else
                UserDialog.AlertAsync("Internal error!", "Warning!", "OK");
                #endif
            }
        }
    }
}