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
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace NSC_ModManager.Controls {
    /// <summary>
    /// Логика взаимодействия для KuramaControl.xaml
    /// </summary>
    public partial class KuramaControl : UserControl {

        // Animasi goyang ekor Kurama DULU pakai WPF Storyboard
        // (RepeatBehavior="Forever" + AutoReverse="True") lewat XAML - lihat
        // catatan di KuramaControl.xaml dan AUDIT_LOG.md bagian 6l untuk
        // alasan kenapa ini diganti jadi DispatcherTimer manual: kombinasi
        // Forever+AutoReverse itu yang memicu
        // System.Windows.Media.Animation.TimeIntervalCollection.
        // ProjectOntoPeriodicFunction, method WPF internal yang terbukti
        // (dari crash_log.txt + log Wine sesi 14:10-14:13) jadi sumber
        // NullReferenceException berulang lalu native ACCESS_VIOLATION di
        // WinNative/Wine. Hasil visual di bawah ini dibuat SAMA PERSIS
        // (From/To/Duration/easing tiap ekor) dengan animasi lama, cuma
        // "mesin"-nya beda: manual timer + RotateTransform.Angle langsung,
        // tidak pernah menyentuh Storyboard/Clock WPF sama sekali.
        private class TailAnim {
            public RotateTransform Transform;
            public double From;
            public double To;
            public double DurationSeconds;
            public double AccelerationRatio;
            public double DecelerationRatio;
            public double ElapsedInLeg;   // detik berjalan di leg saat ini (From->To atau To->From)
            public bool Reversed;         // lagi jalan To->From?
        }

        private DispatcherTimer _timer;
        private readonly List<TailAnim> _tails = new List<TailAnim>();
        private DateTime _lastTick;

        public KuramaControl() {
            InitializeComponent();
            Loaded += KuramaControl_Loaded;
            Unloaded += KuramaControl_Unloaded;
        }

        private void KuramaControl_Loaded(object sender, RoutedEventArgs e) {
            _tails.Clear();
            _tails.Add(new TailAnim {
                Transform = (RotateTransform)KuramaTailImage_2.RenderTransform,
                From = -10, To = 10, DurationSeconds = 1.5,
                AccelerationRatio = 0.2, DecelerationRatio = 0.2
            });
            _tails.Add(new TailAnim {
                Transform = (RotateTransform)KuramaTailImage_3.RenderTransform,
                From = 10, To = -10, DurationSeconds = 2.0,
                AccelerationRatio = 0.3, DecelerationRatio = 0.3
            });
            _tails.Add(new TailAnim {
                Transform = (RotateTransform)KuramaTailImage.RenderTransform,
                From = -14, To = 15, DurationSeconds = 2.4,
                AccelerationRatio = 0.3, DecelerationRatio = 0.3
            });

            foreach (var t in _tails) {
                t.Transform.Angle = t.From;
            }

            if (_timer == null) {
                _timer = new DispatcherTimer(DispatcherPriority.Render) {
                    Interval = TimeSpan.FromMilliseconds(33) // ~30 fps, cukup halus utk animasi loading
                };
                _timer.Tick += Timer_Tick;
            }
            _lastTick = DateTime.UtcNow;
            _timer.Start();
        }

        private void KuramaControl_Unloaded(object sender, RoutedEventArgs e) {
            _timer?.Stop();
        }

        private void Timer_Tick(object sender, EventArgs e) {
            var now = DateTime.UtcNow;
            double dt = (now - _lastTick).TotalSeconds;
            _lastTick = now;
            // Jaga-jaga kalau ada jeda besar (misal window sempat freeze) supaya
            // tidak "meloncat" jauh dalam satu tick.
            if (dt > 0.25) dt = 0.25;

            foreach (var t in _tails) {
                t.ElapsedInLeg += dt;
                double progress = t.ElapsedInLeg / t.DurationSeconds;
                if (progress >= 1.0) {
                    progress = 0.0;
                    t.ElapsedInLeg = 0.0;
                    t.Reversed = !t.Reversed;
                }

                double eased = EaseInOut(progress, t.AccelerationRatio, t.DecelerationRatio);
                double from = t.Reversed ? t.To : t.From;
                double to = t.Reversed ? t.From : t.To;
                t.Transform.Angle = from + (to - from) * eased;
            }
        }

        // Pendekatan sederhana utk meniru AccelerationRatio/DecelerationRatio
        // milik WPF DoubleAnimation (ease-in di awal, ease-out di akhir).
        private static double EaseInOut(double progress, double accelRatio, double decelRatio) {
            if (progress <= 0) return 0;
            if (progress >= 1) return 1;

            double accel = Math.Max(0, Math.Min(1, accelRatio));
            double decel = Math.Max(0, Math.Min(1, decelRatio));
            if (accel + decel > 1) {
                double scale = 1.0 / (accel + decel);
                accel *= scale;
                decel *= scale;
            }
            double linear = 1.0 - accel - decel;

            if (progress < accel && accel > 0) {
                // percepatan: kurva kuadratik naik
                double p = progress / accel;
                return 0.5 * accel * p * p;
            }
            if (progress > 1 - decel && decel > 0) {
                // perlambatan: kurva kuadratik turun
                double p = (1 - progress) / decel;
                double distFromEnd = 0.5 * decel * p * p;
                return 1 - distFromEnd;
            }
            // fase linear di tengah
            double coveredByAccel = 0.5 * accel;
            return coveredByAccel + (progress - accel);
        }
    }
}
