using System;
using System.Drawing;
using System.Windows.Forms;
using AcTools.Render.Base;
using AcTools.Render.Base.Utils;
using AcTools.Render.Kn5Specific;
using AcTools.Windows;
using SlimDX;

namespace AcTools.Render.Wrapper {
    public class Kn5WrapperCameraControlHelper {
        public virtual void CameraMousePan(IKn5ObjectRenderer renderer, double dx, double dy, double height, double width) {
            if (renderer.LockCamera) return;

            var size = 4.0 / Math.Min(height, width);
            dx *= size;
            dy *= size;

            var c = renderer.CameraOrbit;
            if (c != null) {
                c.Target -= (float)dy * Vector3.Cross(c.Look, c.Right) + (float)dx * c.Right;
                // renderer.AutoRotate = false;
                renderer.AutoAdjustTarget = false;
                ((BaseRenderer)renderer).IsDirty = true;
            } else {
                var f = renderer.FpsCamera;
                if (f != null) {
                    f.Position -= (float)dy * Vector3.Cross(f.Look, f.Right) + (float)dx * f.Right;
                    ((BaseRenderer)renderer).IsDirty = true;
                }
            }
        }

        public virtual void CameraMouseRotate(IKn5ObjectRenderer renderer, double dx, double dy, double height, double width) {
            if (renderer.LockCamera) return;

            var size = (renderer.UseFpsCamera ? 140d : 180d) / Math.Min(height, width);
            dx *= size;
            dy *= size;

            renderer.Camera.Pitch(((float)(renderer.UseFpsCamera ? -dy : dy)).ToRadians());
            renderer.Camera.Yaw(((float)(renderer.UseFpsCamera ? -dx : dx)).ToRadians());
            // renderer.AutoRotate = false;
            ((BaseRenderer)renderer).IsDirty = true;
        }

        public virtual void CameraMouseZoom(IKn5ObjectRenderer renderer, double dx, double dy, double height, double width) {
            if (renderer.LockCamera) return;

            var size = 9.0 / Math.Min(height, width);
            dy *= size;

            renderer.Camera.Zoom((float)dy);
            // renderer.AutoRotate = false;
            ((BaseRenderer)renderer).IsDirty = true;
        }

        private bool IsPressed(Keys key) {
            return User32.IsAsyncKeyPressed(key);
        }

        public virtual void OnTick(IKn5ObjectRenderer renderer, float deltaTime) {
            if (IsPressed(Keys.LMenu) || IsPressed(Keys.RMenu)) return;
            if (renderer.LockCamera) return;

            var speed = 0.1f;
            if (IsPressed(Keys.LShiftKey) || IsPressed(Keys.RShiftKey)) speed *= 0.2f;
            if (IsPressed(Keys.LControlKey) || IsPressed(Keys.RControlKey)) speed = 5.0f;

            if (IsPressed(Keys.Up)) {
                renderer.Camera.Walk(speed);
                renderer.AutoRotate = false;
                renderer.AutoAdjustTarget = false;
                ((BaseRenderer)renderer).IsDirty = true;
            }

            if (IsPressed(Keys.Down)) {
                renderer.Camera.Walk(-speed);
                renderer.AutoRotate = false;
                renderer.AutoAdjustTarget = false;
                ((BaseRenderer)renderer).IsDirty = true;
            }

            if (IsPressed(Keys.Left)) {
                renderer.Camera.Strafe(-speed);
                renderer.AutoRotate = false;
                renderer.AutoAdjustTarget = false;
                ((BaseRenderer)renderer).IsDirty = true;
            }

            if (IsPressed(Keys.Right)) {
                renderer.Camera.Strafe(speed);
                renderer.AutoRotate = false;
                renderer.AutoAdjustTarget = false;
                ((BaseRenderer)renderer).IsDirty = true;
            }
        }
    }

    public abstract class FormWrapperMouseBase : FormWrapperBase {
        public bool FormMoving;

        protected FormWrapperMouseBase(BaseRenderer renderer, string title, int width, int height) : base(renderer, title, width, height) {
            Form.MouseMove += OnMouseMove;
            Form.MouseDown += OnMouseDown;
            Form.MouseUp += OnMouseUp;
            Form.MouseWheel += OnMouseWheel;
        }

        protected override void OnLostFocus(object sender, EventArgs e) {
            base.OnLostFocus(sender, e);
            // _moving = false;
        }

        private void OnMouseDown(object sender, MouseEventArgs e) {
            _moved = false;
            _moving = true;
            _down = true;
            _startMousePos = MousePosition;
        }

        private void OnMouseUp(object sender, MouseEventArgs e) {
            if (!_moved) {
                OnClick();
            }
        }

        protected virtual void OnClick() { }

        public Point MousePosition { get; private set; }
        private Point _startMousePos;
        private Point _lastMousePos;

        private bool _moved, _moving, _down;

        protected abstract void OnMouseMove(MouseButtons button, int dx, int dy);

        protected virtual void OnMouseMove(object sender, MouseEventArgs e) {
            if (!Form.Focused) {
                _moving = false;
                return;
            }

            MousePosition = e.Location;

            if (Math.Abs(e.X - _startMousePos.X) > 2 || Math.Abs(e.Y - _startMousePos.Y) > 2) {
                _moved = true;
            }

            if (_moving && !_down) {
                if (e.Button == MouseButtons.Left && FormMoving) {
                    Form.Left += e.X - _lastMousePos.X;
                    Form.Top += e.Y - _lastMousePos.Y;
                    _lastMousePos = e.Location;
                    return;
                }

                OnMouseMove(e.Button, e.X - _lastMousePos.X, e.Y - _lastMousePos.Y);
            }

            _down = false;
            _lastMousePos = e.Location;
        }

        protected abstract void OnMouseWheel(float value);

        protected virtual void OnMouseWheel(object sender, MouseEventArgs e) {
            OnMouseWheel(e.Delta > 0 ? 1f : -1f);
            Renderer.IsDirty = true;
        }
    }

    public class BaseKn5FormWrapper : FormWrapperMouseBase {
        public readonly IKn5ObjectRenderer Kn5ObjectRenderer;

        public bool AutoAdjustTargetOnReset = true;
        public bool InvertMouseButtons = false;

        private Kn5WrapperCameraControlHelper _helper;

        protected Kn5WrapperCameraControlHelper Helper => _helper ?? (_helper = GetHelper());

        protected virtual Kn5WrapperCameraControlHelper GetHelper() {
            return new Kn5WrapperCameraControlHelper();
        }

        public BaseKn5FormWrapper(BaseRenderer renderer, string title, int width, int height) : base(renderer, title, width, height) {
            Kn5ObjectRenderer = (IKn5ObjectRenderer)renderer;
        }

        protected override void OnClick() {}

        protected override void OnMouseMove(MouseButtons button, int dx, int dy) {
            if (button == (InvertMouseButtons ? MouseButtons.Left : MouseButtons.Middle) ||
                    button == MouseButtons.Left && User32.IsAsyncKeyPressed(Keys.Space)) {
                Helper.CameraMousePan(Kn5ObjectRenderer, dx, dy, Form.ClientSize.Width, Form.ClientSize.Height);
            } else if (button == (InvertMouseButtons ? MouseButtons.Right : MouseButtons.Left)) {
                Helper.CameraMouseRotate(Kn5ObjectRenderer, dx, dy, Form.ClientSize.Width, Form.ClientSize.Height);
            } else if (button == (InvertMouseButtons ? MouseButtons.Middle : MouseButtons.Right)
                    && (!Kn5ObjectRenderer.UseFpsCamera || User32.IsAsyncKeyPressed(Keys.LControlKey) || User32.IsAsyncKeyPressed(Keys.RControlKey))) {
                Helper.CameraMouseZoom(Kn5ObjectRenderer, dx, dy, Form.ClientSize.Width, Form.ClientSize.Height);
            }
        }

        protected override void OnMouseMove(object sender, MouseEventArgs e) {
            if (Kn5ObjectRenderer.LockCamera) return;
            base.OnMouseMove(sender, e);
        }

        protected override void OnMouseWheel(float value) {
            var useFpsCamera = Kn5ObjectRenderer.UseFpsCamera;
            var ctrlPressed = User32.IsAsyncKeyPressed(Keys.LControlKey) || User32.IsAsyncKeyPressed(Keys.RControlKey);
            if (!(useFpsCamera ^ ctrlPressed)) {
                Kn5ObjectRenderer.Camera.Zoom(value * (useFpsCamera ? -0.1f : -0.4f));
            } else if (!useFpsCamera) {
                var c = Kn5ObjectRenderer.CameraOrbit;
                if (c == null) return;
                Kn5ObjectRenderer.ChangeCameraFov(c.FovY - value * 0.05f);
            }
        }

        protected override void OnMouseWheel(object sender, MouseEventArgs e) {
            if (Kn5ObjectRenderer.LockCamera) return;
            base.OnMouseWheel(sender, e);
        }

        protected override void OnTick(object sender, TickEventArgs args) {
            base.OnTick(sender, args);
            if (Form.Focused) {
                Helper.OnTick(Kn5ObjectRenderer, args.DeltaTime);
            }
        }

        protected override void OnKeyUp(object sender, KeyEventArgs args) {
            base.OnKeyUp(sender, args);
            if (args.Handled) return;

            if (args.Alt && args.KeyCode == Keys.Enter || args.KeyCode == Keys.F11) {
                args.Handled = true;
                ToggleFullscreen();
                return;
            }

            switch (args.KeyCode) {
                case Keys.Home:
                    if (!args.Control && !args.Alt && !args.Shift && !Kn5ObjectRenderer.LockCamera) {
                        Kn5ObjectRenderer.ResetCamera();
                        if (AutoAdjustTargetOnReset) {
                            Kn5ObjectRenderer.AutoAdjustTarget = true;
                        }
                    }
                    break;

                case Keys.Tab:
                    if (!args.Control && !args.Alt && !args.Shift) {
                        Renderer.SyncInterval = !Renderer.SyncInterval;
                    }
                    break;

                case Keys.L:
                    if (!args.Shift) {
                        if (!args.Control) {
                            Kn5ObjectRenderer.CarLightsEnabled = !Kn5ObjectRenderer.CarLightsEnabled;
                            if (!args.Alt) {
                                Kn5ObjectRenderer.CarBrakeLightsEnabled = Kn5ObjectRenderer.CarLightsEnabled;
                            }
                        } else if (!args.Alt){
                            Kn5ObjectRenderer.CarBrakeLightsEnabled = !Kn5ObjectRenderer.CarBrakeLightsEnabled;
                        }

                        Renderer.IsDirty = true;
                    }
                    break;

                case Keys.Space:
                    if (!args.Control && !args.Alt && !args.Shift && !Kn5ObjectRenderer.LockCamera) {
                        Kn5ObjectRenderer.AutoRotate = !Kn5ObjectRenderer.AutoRotate;
                    }
                    break;

                case Keys.H:
                    if (args.Control && !args.Alt && !args.Shift) {
                        Kn5ObjectRenderer.VisibleUi = !Kn5ObjectRenderer.VisibleUi;
                        Renderer.IsDirty = true;
                    }
                    break;

                case Keys.PageUp:
                    if (!args.Control && !args.Alt && !args.Shift) {
                        Kn5ObjectRenderer.SelectPreviousSkin();
                    }
                    break;

                case Keys.PageDown:
                    if (!args.Control && !args.Alt && !args.Shift) {
                        Kn5ObjectRenderer.SelectNextSkin();
                    }
                    break;
            }
        }
    }
}