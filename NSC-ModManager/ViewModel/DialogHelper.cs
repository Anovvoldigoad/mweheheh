using Microsoft.WindowsAPICodePack.Dialogs;
using System;
using System.Diagnostics;
using System.Windows;

namespace NSC_ModManager.ViewModel
{
    /// <summary>
    /// Helper folder-picker yang aman dipakai di Winlator/Wine.
    /// CommonOpenFileDialog (WindowsAPICodePack, dialog gaya Vista berbasis
    /// COM IFileDialog) kadang gagal/crash di Wine karena implementasi shell
    /// COM interface-nya tidak selalu lengkap. Kalau itu terjadi, otomatis
    /// fallback ke System.Windows.Forms.FolderBrowserDialog (dialog folder
    /// klasik, jauh lebih tua & lebih luas didukung Wine).
    /// </summary>
    public static class DialogHelper
    {
        public static bool TrySelectFolder(string title, out string selectedPath)
        {
            selectedPath = null;
            try
            {
                var dialog = new CommonOpenFileDialog
                {
                    IsFolderPicker = true,
                    Title = title
                };
                CommonFileDialogResult result = dialog.ShowDialog();
                if (result == CommonFileDialogResult.Ok)
                {
                    selectedPath = dialog.FileName;
                    return true;
                }
                return false; // user cancel - jangan fallback, memang tidak jadi pilih
            }
            catch (Exception ex)
            {
                Debug.WriteLine("CommonOpenFileDialog gagal, fallback ke FolderBrowserDialog: " + ex);
                try
                {
                    using (var fallbackDialog = new System.Windows.Forms.FolderBrowserDialog())
                    {
                        fallbackDialog.Description = title;
                        if (fallbackDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                        {
                            selectedPath = fallbackDialog.SelectedPath;
                            return true;
                        }
                        return false;
                    }
                }
                catch (Exception ex2)
                {
                    Debug.WriteLine("FolderBrowserDialog fallback juga gagal: " + ex2);
                    MessageBox.Show(
                        "Tidak bisa membuka dialog pemilih folder di lingkungan ini.\n" + ex2.Message,
                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }
            }
        }
    }
}
