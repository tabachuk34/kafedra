﻿using KafedraApp.Commands;
using KafedraApp.Extensions;
using KafedraApp.Helpers;
using KafedraApp.Models;
using KafedraApp.Services;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace KafedraApp.ViewModels
{
	public class SubjectsViewModel : BindableBase
	{
		#region Fields

		private readonly IDataService _dataService;
		private readonly IDialogService _dialogService;

		#endregion

		#region Properties

		public ObservableCollection<Subject> Subjects => _dataService.Subjects;

		public bool IsSubjectsEmpty => Subjects?.Any() != true;

		#endregion

		#region Constructors

		public SubjectsViewModel()
		{
			_dataService = Container.Resolve<IDataService>();
			_dialogService = Container.Resolve<IDialogService>();

			AddSubjectCommand = new DelegateCommand(AddSubject);
			EditSubjectCommand = new DelegateCommand<Subject>(EditSubject);
			DeleteSubjectCommand = new DelegateCommand<Subject>(DeleteSubject);
			ImportSubjectsCommand = new DelegateCommand(ImportSubjects);

			Subjects.CollectionChanged += SubjectsChanged;
		}

		#endregion

		#region Commands

		public ICommand ImportSubjectsCommand { get; set; }
		public ICommand AddSubjectCommand { get; set; }
		public ICommand EditSubjectCommand { get; set; }
		public ICommand DeleteSubjectCommand { get; set; }

		#endregion

		#region Methods

		private async void AddSubject()
		{
			if (IsBusy)
				return;
			IsBusy = true;

			var subject = await _dialogService.ShowSubjectForm();

			if (subject != null)
				Subjects?.Add(subject);

			IsBusy = false;
		}

		private async void EditSubject(Subject subject)
		{
			if (IsBusy)
				return;
			IsBusy = true;

			subject = await _dialogService.ShowSubjectForm(subject.Clone() as Subject);

			if (subject != null)
			{
				var id = Subjects.IndexOf(Subjects.GetById(subject.Id));
				Subjects[id] = subject;
			}

			IsBusy = false;
		}

		private async void DeleteSubject(Subject subject)
		{
			if (IsBusy)
				return;
			IsBusy = true;

			var res = await _dialogService.ShowQuestion(
				$"Ви дійсно бажаєте видалити { subject.Name }?");

			if (res)
				Subjects.Remove(subject);

			IsBusy = false;
		}

		private void ImportSubjects()
		{
			var dialog = new OpenFileDialog
			{
				Filter = "Excel files|*.xls;*.xlsx;*.xlsm",
				Multiselect = true
			};

			if (dialog.ShowDialog() != true)
				return;

			_dialogService.ShowSubjectImportPopup();

			Task.Run(async () =>
			{
				var subjects = ExcelHelper.GetSubjects(dialog.FileNames);

				foreach (var subject in subjects)
				{
					await Task.Delay(80);

					Application.Current?.Dispatcher?.Invoke(() =>
						Subjects.Add(subject));
				}
			});
		}

		#endregion

		#region Event Handlers

		private void SubjectsChanged(object sender, NotifyCollectionChangedEventArgs e)
		{
			OnPropertyChanged(nameof(IsSubjectsEmpty));
		}

		#endregion
	}
}
