using System.Numerics;
using System.Runtime.InteropServices;

namespace Sample
{
    public class Camera
    {
        private float _fov = 1f;
        private float _near = 1f;
        private float _far = 1000f;
        private Matrix4x4 _viewMatrix;
        private Matrix4x4 _projectionMatrix;
        private Vector3 _position;
        private Vector3 _lookDirection;
        private float _yaw;
        private float _pitch;
        private float _windowWidth;
        private float _windowHeight;

        public event Action<Matrix4x4> ProjectionChanged;
        public event Action<Matrix4x4> ViewChanged;

        public Camera()
        {
            UpdatePerspectiveMatrix();
            UpdateViewMatrix();
        }


        public Matrix4x4 ViewMatrix => _viewMatrix;
        public Matrix4x4 ProjectionMatrix => _projectionMatrix;
        public Vector3 Position { get => _position; set { _position = value; UpdateViewMatrix(); } }
        public Vector3 LookDirection => _lookDirection;
        public float FarDistance => _far;
        public float FieldOfView => _fov;
        public float NearDistance => _near;
        public float AspectRatio => _windowWidth / _windowHeight;
        public float Yaw { get => _yaw; set { _yaw = value; UpdateViewMatrix(); } }
        public float Pitch { get => _pitch; set { _pitch = value; UpdateViewMatrix(); } }


        public void Move(Vector3 motionDir)
        {
            if (motionDir != Vector3.Zero)
            {
                Quaternion lookRotation = Quaternion.CreateFromYawPitchRoll(Yaw, Pitch, 0f);
                motionDir = Vector3.Transform(motionDir, lookRotation);
                _position += motionDir;

                UpdateViewMatrix();
            }
        }

        public void LookAround(Vector2 delta)
        {
            _yaw += delta.X * 0.002f;
            _pitch += delta.Y * 0.002f;
            _pitch = Math.Clamp(_pitch, -1.55f, 1.55f);
         
            UpdateViewMatrix();
        }

        public void WindowResized(float width, float height)
        {
            _windowWidth = width;
            _windowHeight = height;
            UpdatePerspectiveMatrix();
        }

        private void UpdatePerspectiveMatrix()
        {
            _projectionMatrix = Matrix4x4Utils.CreatePerspective(_fov, AspectRatio, _near, _far);
            ProjectionChanged?.Invoke(_projectionMatrix);
        }

        private void UpdateViewMatrix()
        {
            Quaternion lookRotation = Quaternion.CreateFromYawPitchRoll(Yaw, Pitch, 0f);
            Vector3 lookDir = Vector3.Transform(-Vector3.UnitZ, lookRotation);
            _lookDirection = lookDir;
            _viewMatrix = Matrix4x4.CreateLookAt(_position, _position + _lookDirection, Vector3.UnitY);
            ViewChanged?.Invoke(_viewMatrix);
        }

        public CameraInfo GetCameraInfo() => new CameraInfo
        {
            CameraPosition_WorldSpace = _position,
            CameraLookDirection = _lookDirection
        };
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct CameraInfo
    {
        public Vector3 CameraPosition_WorldSpace;
        private float _padding1;
        public Vector3 CameraLookDirection;
        private float _padding2;
    }
}