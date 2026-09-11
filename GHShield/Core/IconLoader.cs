using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;

namespace GHShield.Core
{
    /// <summary>
    /// Loads the embedded icons.
    ///
    /// Resource names are matched by suffix rather than spelled out in full,
    /// because MSBuild derives them from the root namespace and folder path
    /// and the exact string is easy to get wrong. A missing icon returns null,
    /// which Grasshopper handles by drawing its default.
    /// </summary>
    public static class IconLoader
    {
        private static readonly Dictionary<string, Bitmap> Cache =
            new Dictionary<string, Bitmap>();

        public static Bitmap Load(string fileName)
        {
            Bitmap cached;

            if (Cache.TryGetValue(fileName, out cached))
                return cached;

            Bitmap bitmap = null;

            try
            {
                Assembly assembly = Assembly.GetExecutingAssembly();

                foreach (string name in assembly.GetManifestResourceNames())
                {
                    if (!name.EndsWith(fileName, StringComparison.OrdinalIgnoreCase))
                        continue;

                    using (Stream stream = assembly.GetManifestResourceStream(name))
                    {
                        if (stream != null)
                            bitmap = new Bitmap(stream);
                    }

                    break;
                }
            }
            catch (Exception ex)
            {
                Log.Debug($"Could not load icon '{fileName}': {ex.Message}");
            }

            Cache[fileName] = bitmap;

            return bitmap;
        }
    }
}
