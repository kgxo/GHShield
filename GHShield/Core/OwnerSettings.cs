using System;
using System.IO;

namespace GHShield.Core
{
    /// <summary>
    /// Who gets named as having locked a definition, and how to reach them.
    ///
    /// There is no default name. An earlier version fell back to the Windows
    /// account name, which produced stamps reading "Protected by asus" - a
    /// stamp naming a machine names nobody, and is worse than no stamp at all.
    /// Whoever protects a file types who they are, or the line is left out.
    /// Both values are stored as plain text
    /// files rather than in Rhino's settings: there is no API surface to get
    /// wrong, and the user can see and delete them trivially. That matters -
    /// this is their name and their address travelling inside files they send
    /// to other people, and it should never be hidden from them.
    /// </summary>
    public static class OwnerSettings
    {
        private static string _cachedOwner;
        private static string _cachedContact;

        private static string Folder
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "GHShield");
            }
        }

        private static string OwnerFile => Path.Combine(Folder, "owner.txt");

        private static string ContactFile => Path.Combine(Folder, "contact.txt");

        /// <summary>
        /// True once a name has actually been chosen on this machine.
        ///
        /// The difference matters: until this is true, <see cref="Owner"/> is
        /// only a guess at who the user is - their Windows account name - and
        /// that guess should not be quietly sealed into a file they are about
        /// to send to a client.
        /// </summary>
        public static bool IsConfigured
        {
            get
            {
                try
                {
                    // Existence, not content: someone who was asked and chose
                    // to leave it blank has answered, and must not be asked
                    // again on every freeze.
                    return File.Exists(OwnerFile);
                }
                catch (Exception ex)
                {
                    Log.Debug($"Could not check owner setting: {ex.Message}");
                    return false;
                }
            }
        }

        /// <summary>The name written into protected definitions.</summary>
        public static string Owner
        {
            get
            {
                if (_cachedOwner != null)
                    return _cachedOwner;

                _cachedOwner = ReadFile(OwnerFile) ?? string.Empty;

                return _cachedOwner;
            }

            set
            {
                _cachedOwner = (value ?? string.Empty).Trim();

                WriteFile(OwnerFile, _cachedOwner);
            }
        }

        /// <summary>
        /// How to reach the owner. Optional, and blank by default.
        ///
        /// A recipient who cannot change the locked logic needs to know who to
        /// ask. Without this the stamp identifies a person but offers no way
        /// to contact them, which turns a helpful notice into a dead end.
        /// </summary>
        public static string Contact
        {
            get
            {
                if (_cachedContact != null)
                    return _cachedContact;

                _cachedContact = ReadFile(ContactFile) ?? string.Empty;

                return _cachedContact;
            }

            set
            {
                _cachedContact = (value ?? string.Empty).Trim();

                WriteFile(ContactFile, _cachedContact);
            }
        }

        // =====================================================
        // FILES
        // =====================================================

        private static string ReadFile(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    string stored = File.ReadAllText(path).Trim();

                    if (!string.IsNullOrWhiteSpace(stored))
                        return stored;
                }
            }
            catch (Exception ex)
            {
                Log.Debug($"Could not read {Path.GetFileName(path)}: {ex.Message}");
            }

            return null;
        }

        private static void WriteFile(string path, string content)
        {
            try
            {
                string dir = Path.GetDirectoryName(path);

                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                File.WriteAllText(path, content ?? string.Empty);
            }
            catch (Exception ex)
            {
                Log.Debug($"Could not save {Path.GetFileName(path)}: {ex.Message}");
            }
        }
    }
}
