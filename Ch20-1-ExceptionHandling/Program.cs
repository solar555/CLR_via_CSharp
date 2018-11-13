using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ch20_1_ExceptionHandling
{
    internal class Program
    {
        static void Main(string[] args)
        {
        }
    }

    internal static class Mechanics
    {
        public static void SomeMethod()
        {
            try
            {

            }
            catch(InvalidOperationException)
            {

            }
            catch (IOException)
            {

            }
            catch(Exception)
            {
                throw;
            }
            catch
            {
                throw;
            }
            finally
            {

            }
        }
    }
}
