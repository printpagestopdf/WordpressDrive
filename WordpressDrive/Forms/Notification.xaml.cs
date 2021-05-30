using System;
using System.Collections.Generic;
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
using System.Windows.Navigation;
using System.Windows.Shapes;
using Hardcodet.Wpf.TaskbarNotification;


namespace WordpressDrive
{
    /// <summary>
    /// Interaktionslogik für Notification.xaml
    /// </summary>
    public partial class Notification : UserControl
    {
        private bool isClosing = false;

        #region dependency properties

        /// <summary>
        /// Setting the Notification Text to display
        /// </summary>
        public static readonly DependencyProperty BalloonTextProperty =
            DependencyProperty.Register("BalloonText",
                typeof(string),
                typeof(Notification),
                new FrameworkPropertyMetadata(""));

        /// <summary>
        /// A property wrapper for the <see cref="BalloonTextProperty"/>
        /// dependency property:<br/>
        /// Description
        /// </summary>
        public string BalloonText
        {
            get { return (string)GetValue(BalloonTextProperty); }
            set { SetValue(BalloonTextProperty, value); }
        }


        internal Utils.LOGLEVEL LogLevel
        {
            set {
                switch(value)
                {
                    case Utils.LOGLEVEL.ERROR:
                        NotifyImage = new BitmapImage(new Uri(@"/Resources/Exclamation_red_32.png", UriKind.RelativeOrAbsolute));
                        break;
                    case Utils.LOGLEVEL.INFO:
                        NotifyImage = new BitmapImage(new Uri(@"/Resources/Exclamation_yellow_32.png", UriKind.RelativeOrAbsolute));
                        break;
                }
            }
        }



        public ImageSource NotifyImage
        {
            get { return (ImageSource)GetValue(NotifyImageProperty); }
            set { SetValue(NotifyImageProperty, value); }
        }

        // Using a DependencyProperty as the backing store for NotifyImage.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty NotifyImageProperty =
            DependencyProperty.Register("NotifyImage", typeof(ImageSource), typeof(Notification),
                new PropertyMetadata(new BitmapImage(new Uri(@"/Resources/Exclamation_yellow_32.png", UriKind.RelativeOrAbsolute))));


        #endregion

        public Notification()
        {
            InitializeComponent();
            //notify_image.Source = new BitmapImage(new Uri(@"/Resources/Exclamation_red_32.png", UriKind.RelativeOrAbsolute)); ;
            TaskbarIcon.AddBalloonClosingHandler(this, OnBalloonClosing);
        }

        /// <summary>
        /// By subscribing to the <see cref="TaskbarIcon.BalloonClosingEvent"/>
        /// and setting the "Handled" property to true, we suppress the popup
        /// from being closed in order to display the custom fade-out animation.
        /// </summary>
        private void OnBalloonClosing(object sender, RoutedEventArgs e)
        {
            e.Handled = true; //suppresses the popup from being closed immediately
            isClosing = true;
        }


        /// <summary>
        /// Resolves the <see cref="TaskbarIcon"/> that displayed
        /// the balloon and requests a close action.
        /// </summary>
        private void ImgClose_MouseDown(object sender, MouseButtonEventArgs e)
        {
            //the tray icon assigned this attached property to simplify access
            TaskbarIcon taskbarIcon = TaskbarIcon.GetParentTaskbarIcon(this);
            taskbarIcon.CloseBalloon();
        }

        /// <summary>
        /// If the users hovers over the balloon, we don't close it.
        /// </summary>
        private void Grid_MouseEnter(object sender, MouseEventArgs e)
        {
            //if we're already running the fade-out animation, do not interrupt anymore
            //(makes things too complicated for the sample)
            if (isClosing) return;

            //the tray icon assigned this attached property to simplify access
            //TaskbarIcon taskbarIcon = TaskbarIcon.GetParentTaskbarIcon(this);
            //taskbarIcon.ResetBalloonCloseTimer();

        }


        /// <summary>
        /// Closes the popup once the fade-out animation completed.
        /// The animation was triggered in XAML through the attached
        /// BalloonClosing event.
        /// </summary>
        private void OnFadeOutCompleted(object sender, EventArgs e)
        {
            Popup pp = (Popup)Parent;
            pp.IsOpen = false;
        }

    }
}
