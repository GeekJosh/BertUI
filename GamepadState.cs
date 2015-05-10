/* 
 * 
 * adapted from code originally written by provided by Renaud Bédard:
 * http://theinstructionlimit.com/full-xbox-360-gamepad-support-in-c-without-xna
 * 
 */ 

using System;
using SlimDX;
using SlimDX.XInput;
using System.Timers;

namespace BertUI
{
    public class GamepadState
    {
        uint lastPacket;

        public GamepadState(UserIndex userIndex)
        {
            UserIndex = userIndex;
            Controller = new Controller(userIndex);
        }

        public readonly UserIndex UserIndex;
        public readonly Controller Controller;

        public DPadState DPad { get; private set; }
        public ThumbstickState LeftStick { get; private set; }
        public ThumbstickState RightStick { get; private set; }

        public bool A { get; private set; }
        public bool B { get; private set; }
        public bool X { get; private set; }
        public bool Y { get; private set; }

        public bool RightShoulder { get; private set; }
        public bool LeftShoulder { get; private set; }

        public bool Start { get; private set; }
        public bool Back { get; private set; }

        public float RightTrigger { get; private set; }
        public float LeftTrigger { get; private set; }

        public bool Connected
        {
            get { return Controller.IsConnected; }
        }

        public void Vibrate(float leftMotor, float rightMotor)
        {
            Controller.SetVibration(new Vibration
            {
                LeftMotorSpeed = (ushort) (MathHelper.Saturate(leftMotor) * ushort.MaxValue),
                RightMotorSpeed = (ushort) (MathHelper.Saturate(rightMotor) * ushort.MaxValue)
            });
        }

        public void Vibrate(float leftMotor, float rightMotor, double timeMillis)
        {
            Timer timer = new Timer(timeMillis);
            timer.Elapsed += (s, e) => 
            {
                this.Vibrate(0f, 0f);
                timer.Dispose();
            };
            this.Vibrate(leftMotor, rightMotor);
            timer.Start();
        }

        public void Update()
        {
            // If not connected, nothing to update
            if (!Connected) return;

            // If same packet, nothing to update
            State state = Controller.GetState();
            if (lastPacket == state.PacketNumber) return;
            lastPacket = state.PacketNumber;

            var gamepadState = state.Gamepad;

            // Shoulders
            LeftShoulder = (gamepadState.Buttons & GamepadButtonFlags.LeftShoulder) != 0;
            RightShoulder = (gamepadState.Buttons & GamepadButtonFlags.RightShoulder) != 0;

            // Triggers
            LeftTrigger = gamepadState.LeftTrigger / (float) byte.MaxValue;
            RightTrigger = gamepadState.RightTrigger / (float) byte.MaxValue;

            // Buttons
            Start = (gamepadState.Buttons & GamepadButtonFlags.Start) != 0;
            Back = (gamepadState.Buttons & GamepadButtonFlags.Back) != 0;

            A = (gamepadState.Buttons & GamepadButtonFlags.A) != 0;
            B = (gamepadState.Buttons & GamepadButtonFlags.B) != 0;
            X = (gamepadState.Buttons & GamepadButtonFlags.X) != 0;
            Y = (gamepadState.Buttons & GamepadButtonFlags.Y) != 0;

            // D-Pad
            DPad = new DPadState((gamepadState.Buttons & GamepadButtonFlags.DPadUp) != 0,
                                 (gamepadState.Buttons & GamepadButtonFlags.DPadDown) != 0,
                                 (gamepadState.Buttons & GamepadButtonFlags.DPadLeft) != 0,
                                 (gamepadState.Buttons & GamepadButtonFlags.DPadRight) != 0);

            // Thumbsticks
            LeftStick = new ThumbstickState(
                Normalize(gamepadState.LeftThumbX, gamepadState.LeftThumbY, Gamepad.GamepadLeftThumbDeadZone),
                (gamepadState.Buttons & GamepadButtonFlags.LeftThumb) != 0);
            RightStick = new ThumbstickState(
                Normalize(gamepadState.RightThumbX, gamepadState.RightThumbY, Gamepad.GamepadRightThumbDeadZone),
                (gamepadState.Buttons & GamepadButtonFlags.RightThumb) != 0);
        }

        static Vector2 Normalize(short rawX, short rawY, short threshold)
        {
            var value = new Vector2(rawX, rawY);
            var magnitude = value.Length();
            var direction = value / (magnitude == 0 ? 1 : magnitude);

            var normalizedMagnitude = 0.0f;
            if (magnitude - threshold > 0)
                normalizedMagnitude = Math.Min((magnitude - threshold) / (short.MaxValue - threshold), 1);

            return direction * normalizedMagnitude;
        }

        public struct DPadState
        {
            public readonly bool Up, Down, Left, Right;

            public DPadState(bool up, bool down, bool left, bool right)
            {
                Up = up; Down = down; Left = left; Right = right;
            }
        }

        public struct ThumbstickState
        {
            public readonly Vector2 Position;
            public readonly bool Clicked;

            public ThumbstickState(Vector2 position, bool clicked)
            {
                Clicked = clicked;
                Position = position;
            }
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
        
        public override bool Equals(Object obj)
        {
            
            if (obj == null) return false;
            
            GamepadState gp = obj as GamepadState;
            if ((Object)gp == null) return false;

            return (this.Equals(gp));
        }

        public bool Equals(GamepadState gp)
        {
            if ((Object)gp == null) return false;

            return (
                this.UserIndex == gp.UserIndex &&
                this.A == gp.A &&
                this.B == gp.B &&
                this.X == gp.X &&
                this.Y == gp.Y &&
                this.DPad.Up == gp.DPad.Up &&
                this.DPad.Down == gp.DPad.Down &&
                this.DPad.Left == gp.DPad.Left &&
                this.DPad.Right == gp.DPad.Right &&
                this.LeftShoulder == gp.LeftShoulder &&
                this.LeftTrigger == gp.LeftTrigger &&
                this.LeftStick.Clicked == gp.LeftStick.Clicked &&
                this.LeftStick.Position.X == gp.LeftStick.Position.X &&
                this.LeftStick.Position.Y == gp.LeftStick.Position.Y &&
                this.RightShoulder == gp.RightShoulder &&
                this.RightTrigger == gp.RightTrigger &&
                this.RightStick.Clicked == gp.RightStick.Clicked &&
                this.RightStick.Position.X == gp.RightStick.Position.X &&
                this.RightStick.Position.Y == gp.RightStick.Position.Y &&
                this.Back == gp.Back &&
                this.Start == gp.Start
                );
        }
        
    }

    public static class MathHelper
    {
        public static float Saturate(float value)
        {
            return value < 0 ? 0 : value > 1 ? 1 : value;
        }
    }
}
