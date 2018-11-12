using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Ch19_1_NullableValueAttributes
{
    public class Program
    {
        public static void Main(string[] args)
        {
            NullGetType();
            Console.ReadKey();
        }

        /// <summary>
        /// Ch19.1
        /// </summary>
        private static void UsingNullable()
        {
            Nullable<int> x = 5;
            Nullable<int> y = null;
            Console.WriteLine("x: HasValue={0}, Value={1}", x.HasValue, x.Value);
            Console.WriteLine("y: HasValue={0}, Value={1}", y.HasValue, y.Value);
        }

        /// <summary>
        /// Ch19.1
        /// </summary>
        private static void UsingNullablePoint()
        {
            Point? p1 = new Point(1, 1);
            Point? p2 = new Point(2, 2);

            Console.WriteLine("Are points equal? " + (p1 == p2).ToString());
            Console.WriteLine("Are points not equal? " + (p1 != p2).ToString());
        }

        /// <summary>
        /// 19.3.3
        /// CLR lie
        /// </summary>
        private static void NullGetType()
        {
            int? x = 5;
            Console.WriteLine(x.GetType());
        }

        private static void NullCallInterface()
        {
            int? n = 5;
            int result = ((IComparable)n).CompareTo(5);
            Console.WriteLine(result);
        }
    }

    /// <summary>
    /// Ch19.1
    /// </summary>
    internal struct Point
    {
        private int m_x, m_y;
        public Point(int x, int y)
        {
            m_x = x;
            m_y = y;
        }

        public static bool operator==(Point p1, Point p2)
        {
            return (p1.m_x == p2.m_x) && (p1.m_y == p2.m_y);
        }

        public static bool operator!=(Point p1, Point p2)
        {
            return !(p1 == p2);
        }
    }
}
