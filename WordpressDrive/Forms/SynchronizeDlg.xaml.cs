using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace WordpressDrive
{
    /// <summary>
    /// Interaktionslogik für Synchronize.xaml
    /// </summary>
    public partial class SynchronizeDlg : Window
    {
        private bool synchronized = false;
        private Synchronizer sync;
        private Settings.HostSettings hostSettings;

        internal SynchronizeDlg(Synchronizer sync, Settings.HostSettings hostSettings)
        {
            this.sync = sync;
            this.hostSettings = hostSettings;

            if (hostSettings.ShowSyncDlg)
            {
                this.WindowStyle = WindowStyle.ToolWindow;
            }
            else
            {
                this.WindowStyle = WindowStyle.SingleBorderWindow;
                this.ShowInTaskbar = true;
                this.WindowState = WindowState.Minimized;
            }

            InitializeComponent();

            lvSyncList.ItemsSource = sync.SyncItems;
            pbSync.DataContext = sync.SyncProgress;

            if (!hostSettings.ShowSyncDlg)
                btSync.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        }

        private void BtSync_Click(object sender, RoutedEventArgs e)
        {
            TaskbarItemInfo.ProgressState = System.Windows.Shell.TaskbarItemProgressState.Normal;

            Task.Run(() => sync.Synchronize(false))
            .ContinueWith((t) => this.SynchronisationFinalized(t), TaskScheduler.FromCurrentSynchronizationContext());
        }

        public void SynchronisationFinalized(Task t)
        {
            synchronized = true;
            if (hostSettings.AutoCloseSyncDlg || !hostSettings.ShowSyncDlg)
                Close();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);
            if (synchronized) return;

            if (MessageBox.Show(Properties.Resources.msgbox_omit_sync_question,
               Properties.Resources.warning,
                MessageBoxButton.OKCancel,
                MessageBoxImage.Stop) != MessageBoxResult.OK)
                e.Cancel = true;
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            sync.ClearSyncList();
        }

        private void BtCancelSync_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void PbSync_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            
            TaskbarItemInfo.ProgressValue = pbSync.Value / pbSync.Maximum;
        }
    }


}
