﻿using KafedraApp.ViewModels;

namespace KafedraApp.Views
{
	public partial class TeachersView
	{
		public TeachersView()
		{
			InitializeComponent();
			DataContext = new TeachersViewModel();
		}
	}
}
