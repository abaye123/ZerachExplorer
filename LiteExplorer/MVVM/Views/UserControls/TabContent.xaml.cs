using System;
using System.Windows;
using System.Linq;
using System.Collections.Generic;
using Wpf.Ui.Markup;
using System.IO;
using LiteExplorer.MVVM.Views.Windows;


namespace LiteExplorer.MVVM.Views.UserControls;

public partial class TabContent
{
    public TabContent() => InitializeComponent();

    private void Button_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        about aboutForm = new about();
        aboutForm.ShowDialog();
    }

    private void Button_Click_1(object sender, RoutedEventArgs e)
    {
        var filesToDelete = new List<string>
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"com.example\otzaria\tabs.isar"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"com.example\otzaria\history.isar"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"com.example\otzaria\bookmarks.isar")
            };

        string result = DeleteFiles(filesToDelete);
        Console.WriteLine(result);
    }


    private static string DeleteFiles(List<string> filePaths)
    {
        var successList = new List<string>();
        var failureList = new List<string>();

        foreach (var filePath in filePaths)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    successList.Add(filePath);
                }
                else
                {
                    //failureList.Add($"{filePath} (File not found)");
                }
            }
            catch (Exception ex)
            {
                failureList.Add($"{filePath} (Error: {ex.Message})");
            }
        }

        if (failureList.Any())
        {
            return $"לא ניתן היה למחוק חלק מהקבצים:\n{string.Join("\n", failureList)}";
        }
        else
        {
            return "success";
        }
    }
}
