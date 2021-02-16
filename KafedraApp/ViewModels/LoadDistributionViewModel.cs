﻿using KafedraApp.Commands;
using KafedraApp.Helpers;
using KafedraApp.Models;
using KafedraApp.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace KafedraApp.ViewModels
{
	public class LoadDistributionViewModel : BindableBase
	{
		#region Fields

		private readonly IDataService _dataService;
		private readonly IDialogService _dialogService;

		#endregion

		#region Properties

		private Teacher _currentTeacher;
		public Teacher CurrentTeacher
		{
			get => _currentTeacher;
			set => SetProperty(ref _currentTeacher, value);
		}

		public ObservableCollection<Teacher> Teachers =>
			_dataService.Teachers;

		private ObservableCollection<LoadItem> _notDistributedLoad;
		public ObservableCollection<LoadItem> NotDistributedLoad
		{
			get => _notDistributedLoad;
			set
			{
				if (_notDistributedLoad != null)
					_notDistributedLoad.CollectionChanged -= NotDistributedLoadChanged;

				SetProperty(ref _notDistributedLoad, value);
				OnPropertyChanged(nameof(NotDistributedLoadHours));

				if (_notDistributedLoad != null)
					_notDistributedLoad.CollectionChanged += NotDistributedLoadChanged;
			}
		}

		private ObservableCollection<LoadItem> _notDistributedLoadToShow;
		public ObservableCollection<LoadItem> NotDistributedLoadToShow
		{
			get => _notDistributedLoadToShow;
			set => SetProperty(ref _notDistributedLoadToShow, value);
		}

		public List<LoadItem> FilteredNotDistributedLoad
		{
			get
			{
				if (NotDistributedLoad?.Any() != true)
				{
					return NotDistributedLoad?.ToList();
				}

				IEnumerable<LoadItem> filteredLoadItems = NotDistributedLoad;

				if (!string.IsNullOrWhiteSpace(SearchText))
				{
					filteredLoadItems = NotDistributedLoad
						.Where(x => x.Subject.ToLower().Contains(SearchText.ToLower()))
						.ToList();
				}

				switch (SelectedSortingField)
				{
					case "Предмет":
						filteredLoadItems = OrderBy(filteredLoadItems, x => x.Subject);
						break;
					case "Тип роботи":
						filteredLoadItems = OrderBy(filteredLoadItems, x => x.Type);
						break;
					case "К-сть годин":
						filteredLoadItems = OrderBy(filteredLoadItems, x => x.Hours);
						break;
					case "Група":
						filteredLoadItems = OrderBy(filteredLoadItems, x => x.Group);
						break;
					case "Семестр":
						filteredLoadItems = OrderBy(filteredLoadItems, x => x.Semester);
						break;
				}

				return filteredLoadItems.ToList();
			}
		}

		public double NotDistributedLoadHours => NotDistributedLoad?.Sum(x => x.Hours) ?? 0;

		private string _searchText;
		public string SearchText
		{
			get => _searchText;
			set
			{
				SetProperty(ref _searchText, value);
				OnSearchTextChanged();
			}
		}

		public List<string> SortingFields => new List<string>
		{
			"Предмет",
			"Тип роботи",
			"К-сть годин",
			"Група",
			"Семестр"
		};

		private string _selectedSortingField;
		public string SelectedSortingField
		{
			get => _selectedSortingField;
			set
			{
				SetProperty(ref _selectedSortingField, value);
				OnSelectedSortingFieldChanged();
			}
		}

		#endregion

		#region Commands

		public ICommand SwitchTeacherCommand { get; private set; }
		public ICommand AssignLoadCommand { get; private set; }
		public ICommand UnassignLoadCommand { get; private set; }
		public ICommand FormLoadCommand { get; private set; }
		public ICommand ResetLoadCommand { get; private set; }

		#endregion

		#region Constructors

		public LoadDistributionViewModel()
		{
			_dataService = Container.Resolve<IDataService>();
			_dialogService = Container.Resolve<IDialogService>();

			SwitchTeacherCommand = new DelegateCommand<Teacher>(SwitchTeacher);
			AssignLoadCommand = new DelegateCommand<LoadItem>(AssignLoad);
			UnassignLoadCommand = new DelegateCommand<LoadItem>(UnassignLoad);
			FormLoadCommand = new DelegateCommand(FormLoad);
			ResetLoadCommand = new DelegateCommand(async () => await ResetLoad());

			CurrentTeacher = _dataService.Teachers.FirstOrDefault();
			SelectedSortingField = SortingFields[0];

			var load = _dataService.GetLoadItems();
			var distributedLoad = Teachers?.Where(x => x?.LoadItems?.Count > 0)
				.SelectMany(x => x?.LoadItems).ToList();
			var notDistributedLoad = load.Except(distributedLoad).ToList();

			NotDistributedLoad = new ObservableCollection<LoadItem>(notDistributedLoad);
			
			InitNotDistributedLoadToShow();
		}

		#endregion

		#region Methods

		private void SwitchTeacher(Teacher teacher)
		{
			CurrentTeacher = teacher;
		}

		private void AssignLoad(LoadItem loadItem)
		{
			if (CurrentTeacher.LoadItems == null)
				CurrentTeacher.LoadItems = new ObservableCollection<LoadItem>();

			CurrentTeacher.LoadItems.Add(loadItem);
			NotDistributedLoad.Remove(loadItem);
		}

		private void UnassignLoad(LoadItem loadItem)
		{
			CurrentTeacher.LoadItems.Remove(loadItem);
			NotDistributedLoad.Add(loadItem);
		}

		private void InitNotDistributedLoadToShow()
		{
			if (FilteredNotDistributedLoad?.Any() == true)
			{
				NotDistributedLoadToShow = new ObservableCollection<LoadItem>(
					FilteredNotDistributedLoad.Take(Math.Min(10, FilteredNotDistributedLoad.Count)));
			}
			else
			{
				NotDistributedLoadToShow = new ObservableCollection<LoadItem>();
			}
		}

		public void AddNotDistributedLoadItemToShow()
		{
			if (NotDistributedLoadToShow.Count >= FilteredNotDistributedLoad.Count)
				return;

			NotDistributedLoadToShow
				.Add(FilteredNotDistributedLoad[NotDistributedLoadToShow.Count]);
		}

		private async void FormLoad()
		{
			if (!await ResetLoad())
				return;

			foreach (var teacher in Teachers)
			{
				if (teacher.LoadItems == null)
					teacher.LoadItems = new ObservableCollection<LoadItem>();

				foreach (var subject in teacher.SubjectsSpecializesIn)
				{
					var loadItems = NotDistributedLoad.Where(x => x.Subject == subject).ToList();

					for (int i = 0; i < loadItems.Count && teacher.LoadHours < teacher.RateHours; ++i)
					{
						var loadItem = loadItems[i];
						IEnumerable<LoadItem> relatedLoadItems;

						switch (loadItems[i].Type)
						{
							case LoadItemTypes.Lectures:
								teacher.LoadItems.Add(loadItem);
								NotDistributedLoad.Remove(loadItem);

								relatedLoadItems = loadItems.Where(x =>
									x.Subject == loadItem.Subject
									&& x.Semester == loadItem.Semester
									&& (x.Type == LoadItemTypes.Exam || x.Type == LoadItemTypes.IndividualTasks));

								foreach (var relatedLoadItem in relatedLoadItems)
								{
									if (NotDistributedLoad.Contains(relatedLoadItem))
									{
										NotDistributedLoad.Remove(relatedLoadItem);
										teacher.LoadItems.Add(relatedLoadItem);
									}
								}
								break;
							case LoadItemTypes.LaboratoryWork:
							case LoadItemTypes.PracticalWork:
								teacher.LoadItems.Add(loadItem);
								NotDistributedLoad.Remove(loadItem);

								relatedLoadItems = loadItems.Where(x =>
									x.Subject == loadItem.Subject
									&& x.Semester == loadItem.Semester
									&& (x.Type == LoadItemTypes.Test || x.Type == LoadItemTypes.ControlWork));

								foreach (var relatedLoadItem in relatedLoadItems)
								{
									if (NotDistributedLoad.Contains(relatedLoadItem))
									{
										NotDistributedLoad.Remove(relatedLoadItem);
										teacher.LoadItems.Add(relatedLoadItem);
									}
								}
								break;
						}
					}
				}
			}
		}

		private async Task<bool> ResetLoad()
		{
			if (!Teachers.Any(x => x.LoadItems?.Any() == true))
				return true;

			if (!await _dialogService.ShowQuestion("Ви дійсно бажаєте скинути поточний розподіл?"))
				return false;

			NotDistributedLoad = new ObservableCollection<LoadItem>(_dataService.GetLoadItems());
			InitNotDistributedLoadToShow();

			foreach (var teacher in Teachers)
			{
				teacher.LoadItems = null;
			}

			return true;
		}

		private IEnumerable<LoadItem> OrderBy<TKey>(
			IEnumerable<LoadItem> loadItems,
			Func<LoadItem, TKey> keySelector,
			bool byDescending = false)
		{
			if (byDescending)
			{
				return loadItems.OrderByDescending(keySelector);
			}

			return loadItems.OrderBy(keySelector);
		}

		#endregion

		#region Event Handlers

		private void NotDistributedLoadChanged(object sender, NotifyCollectionChangedEventArgs e)
		{
			OnPropertyChanged(nameof(NotDistributedLoadHours));

			switch (e.Action)
			{
				case NotifyCollectionChangedAction.Add:
					if (NotDistributedLoadToShow.Count >= 10)
						break;

					var loadItem = e.NewItems[0] as LoadItem;
					var index = FilteredNotDistributedLoad.IndexOf(loadItem);

					if (index >= 0 && index <= NotDistributedLoadToShow.Count)
					{
						NotDistributedLoadToShow.Insert(index, loadItem);
					}
					break;
				case NotifyCollectionChangedAction.Remove:
					loadItem = e.OldItems[0] as LoadItem;
					NotDistributedLoadToShow.Remove(loadItem);
					AddNotDistributedLoadItemToShow();
					break;
				case NotifyCollectionChangedAction.Replace:
					var newSubject = e.NewItems[0] as LoadItem;

					index = FilteredNotDistributedLoad.IndexOf(newSubject);

					if (index >= 0 && index <= NotDistributedLoadToShow.Count)
					{
						NotDistributedLoadToShow.RemoveAt(index);
						NotDistributedLoadToShow.Insert(index, newSubject);
					}
					break;
				case NotifyCollectionChangedAction.Move:
					break;
				case NotifyCollectionChangedAction.Reset:
					NotDistributedLoadToShow.Clear();
					break;
			}
		}

		private void OnSearchTextChanged()
		{
			InitNotDistributedLoadToShow();
			//OnPropertyChanged(nameof(IsSubjectsEmpty));
		}

		private void OnSelectedSortingFieldChanged()
		{
			InitNotDistributedLoadToShow();
		}

		#endregion
	}
}
