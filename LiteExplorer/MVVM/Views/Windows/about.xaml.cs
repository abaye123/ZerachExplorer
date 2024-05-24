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
using System.Windows.Shapes;
using Wpf.Ui.Markup;

namespace LiteExplorer.MVVM.Views.Windows
{
    /// <summary>
    /// Interaction logic for about.xaml
    /// </summary>
    public partial class about : Window
    {
        public about()
        {
            InitializeComponent();
        }





        private static void ChangeTheme(string theme)
        {
            var appResources = Application.Current.Resources;
            var themesDictionary = appResources.MergedDictionaries
                                              .OfType<ResourceDictionary>()
                                              .FirstOrDefault(d => d is ThemesDictionary);

            if (themesDictionary != null)
            {
                var themeProperty = themesDictionary.GetType().GetProperty("Theme");
                if (themeProperty != null)
                {
                    var currentTheme = (string)themeProperty.GetValue(themesDictionary);
                    if (currentTheme != theme)
                    {
                        themeProperty.SetValue(themesDictionary, theme);
                    }
                }
                else
                {
                    MessageBox.Show("Theme property not found in ThemesDictionary.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                MessageBox.Show("ThemesDictionary not found.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            //ChangeTheme("Dark");
            this.Close();
        }
    }
}
