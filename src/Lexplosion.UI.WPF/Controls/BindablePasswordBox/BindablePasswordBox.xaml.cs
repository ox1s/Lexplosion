using Lexplosion.UI.WPF.Extensions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System;
using System.Windows.Media.Animation;

namespace Lexplosion.UI.WPF.Controls
{
    /// <summary>
    /// Логика взаимодействия для BindablePasswordBox.xaml
    /// </summary>
    public partial class BindablePasswordBox : UserControl
    {
        private bool _isUpdating;
        private bool _isPasswordVisible;

        private static readonly DoubleAnimation _fadeIn  = new DoubleAnimation(1, TimeSpan.FromSeconds(0.35));
        private static readonly DoubleAnimation _fadeOut = new DoubleAnimation(0, TimeSpan.FromSeconds(0.35));


        #region Dependency Properties


        public static readonly DependencyProperty PasswordProperty =
            DependencyProperty.Register("Password", typeof(string), typeof(BindablePasswordBox),
                new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                    PasswordPropertyChanged, null, false, UpdateSourceTrigger.PropertyChanged));

        public string Password
        {
            get { return (string)GetValue(PasswordProperty); }
            set { SetValue(PasswordProperty, value); }
        }

        public static readonly DependencyProperty PlaceholderProperty =
            DependencyProperty.Register("Placeholder", typeof(string), typeof(BindablePasswordBox),
                new PropertyMetadata(string.Empty));

        public string Placeholder
        {
            get { return (string)GetValue(PlaceholderProperty); }
            set { SetValue(PlaceholderProperty, value); }
        }

        #endregion Dependency Properties


        #region Constructors


        public BindablePasswordBox()
        {
            InitializeComponent();
        }


        #endregion Constructors


        #region Focus animation


        private void Input_GotFocus(object sender, RoutedEventArgs e)
        {
            BorderFocused.BeginAnimation(OpacityProperty, _fadeIn);
        }

        private void Input_LostFocus(object sender, RoutedEventArgs e)
        {
            BorderFocused.BeginAnimation(OpacityProperty, _fadeOut);
        }


        #endregion Focus animation


        #region Password sync


        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (_isUpdating) return;
            _isUpdating = true;
            Password = passwordBox.Password;
            visibleBox.Text = passwordBox.Password;
            _isUpdating = false;
        }

        private void VisibleBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdating) return;
            _isUpdating = true;
            Password = visibleBox.Text;
            passwordBox.Password = visibleBox.Text;
            _isUpdating = false;
        }

        private static void PasswordPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is BindablePasswordBox control)
                control.UpdatePassword();
        }

        private void UpdatePassword()
        {
            if (_isUpdating) return;
            _isUpdating = true;
            passwordBox.Password = Password ?? string.Empty;
            visibleBox.Text = Password ?? string.Empty;
            _isUpdating = false;
        }


        #endregion Password sync


        #region Toggle visibility


        private void ToggleButton_Click(object sender, RoutedEventArgs e)
        {
            _isPasswordVisible = !_isPasswordVisible;

            if (_isPasswordVisible)
            {
                visibleBox.Text = passwordBox.Password;
                passwordBox.Visibility = Visibility.Collapsed;
                visibleBox.Visibility = Visibility.Visible;
                visibleBox.Focus();
                visibleBox.CaretIndex = visibleBox.Text.Length;
                PathExtensions.SetStringKeyData(eyeIcon, "EyeVisible");
            }
            else
            {
                passwordBox.Password = visibleBox.Text;
                visibleBox.Visibility = Visibility.Collapsed;
                passwordBox.Visibility = Visibility.Visible;
                passwordBox.Focus();
                PathExtensions.SetStringKeyData(eyeIcon, "EyeHidden");
            }
        }


        #endregion Toggle visibility
    }
}
