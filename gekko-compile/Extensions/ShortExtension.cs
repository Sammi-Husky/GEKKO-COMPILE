﻿namespace System
{
    public static class SingleExtension
    {
        public static unsafe float Reverse(this float value)
        {
            *(uint*)(&value) = ((uint*)&value)->Reverse();
            return value;
        }

        //private static double _double2fixmagic = 68719476736.0f * 1.5f;
        //public static unsafe Int32 ToInt32(this Single value)
        //{
        //    double v = value + _double2fixmagic;
        //    return *((int*)&v) >> 16;
        //}
    }
}
