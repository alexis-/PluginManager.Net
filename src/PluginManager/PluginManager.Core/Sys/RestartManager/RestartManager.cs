#region License & Metadata

// The MIT License (MIT)
// 
// Permission is hereby granted, free of charge, to any person obtaining a
// copy of this software and associated documentation files (the "Software"),
// to deal in the Software without restriction, including without limitation
// the rights to use, copy, modify, merge, publish, distribute, sublicense,
// and/or sell copies of the Software, and to permit persons to whom the
// Software is furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
// DEALINGS IN THE SOFTWARE.
// 
// 
// Modified On:  2020/03/06 12:14
// Modified By:  Alexis

#endregion




using System;
using System.ComponentModel;

namespace PluginManager.Sys.RestartManager
{
  [EditorBrowsable(EditorBrowsableState.Never)]
  internal static partial class RestartManager
  {
    #region Methods

    public static RM_PROCESS_INFO[] FindLockerProcesses(string path)
    {
      if (NativeMethods.RmStartSession(out var handle, 0, Guid.NewGuid().ToString()) != RmResult.ERROR_SUCCESS)
        throw new Exception("Could not begin session. Unable to determine file lockers.");

      try
      {
        string[] resources = { path }; // Just checking on one resource.

        if (NativeMethods.RmRegisterResources(handle, (uint)resources.LongLength, resources, 0, null, 0, null) != RmResult.ERROR_SUCCESS)
          throw new Exception("Could not register resource.");

        // The first try is done expecting at most ten processes to lock the file.
        uint     arraySize = 10;
        RmResult result;
        do
        {
          var array = new RM_PROCESS_INFO[arraySize];
          result = NativeMethods.RmGetList(handle, out var arrayCount, ref arraySize, array, out _);

          if (result == RmResult.ERROR_SUCCESS)
          {
            // Adjust the array length to fit the actual count.
            Array.Resize(ref array, (int)arrayCount);
            return array;
          }
          else if (result == RmResult.ERROR_MORE_DATA)
          {
            // We need to initialize a bigger array. We only set the size, and do another iteration.
            // (the out parameter arrayCount contains the expected value for the next try)
            arraySize = arrayCount;
          }
          else
          {
            throw new Exception("Could not list processes locking resource. Failed to get size of result.");
          }
        } while (result != RmResult.ERROR_SUCCESS);
      }
      finally
      {
        NativeMethods.RmEndSession(handle);
      }

      return new RM_PROCESS_INFO[0];
    }

    #endregion
  }
}
