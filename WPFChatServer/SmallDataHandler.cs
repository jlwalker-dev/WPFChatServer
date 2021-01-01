/*
 * This is for keeping small data files that will are expected to contain no more than a hundred or so records.  
 * It is not designed for serious data work, but designed to be a very lightweight data handler that cam be0
 * dropped into any system.
 * 
 * THERE IS NO FILE SHARING!  All data files are opened exclusively and there is currently no 
 * substantive error handling for files in use.  I expect at some point I may do that, but
 * I'm in no hurry as I do not see this code as anything but a single use data environments.
 * 
 * History
 *  2020-11-20
 *      Started this class and got it going in about 6 hours.  Nothing impressive, just simple
 *      data handling with pipe delimeters.
 *      
 *      SmallFileHandlerStructure class provides a useful data link to this code
 *      
 *  2020-11-24
 *      Some clean up of code and misc improvements to make it more generic and useful for
 *      small projects of any kind.
 *      
 * 2020-11-28
 *      Added pack command
 *      
 *      Added fieldname support
 *          sdh.SetFieldNames(comma delimited field list)
 *          rec.GetField(fieldname)
 *          rec.SetField(fieldname, value)
 *          rec.GetFieldIdx(fieldname)
 *          rec.GetFieldNamesArray()
 *          sdh.GetFieldIdx(fieldname)
 *          sdh.GetFieldNamesArray()
 * 
 * 
 */
using System;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace WPFChatServer
{
    class SmallDataHandler
    {
        private int recLen = 14;
        private int recCount = 0;
        private int fieldCount = 0;
        private string fieldNames = string.Empty;
        private FileStream fs;

        public static readonly string dataExt = ".sdt";

        // Search Types
        public enum Match { Exact, ExactCaseInsensitive, PartialCaseInsensitive }

        // 
        public string TableName { get => Path.GetFileName(FileName); }
        public int RecCount { get => recCount; }
        public int ErrorCode { get; private set; } = 0;
        public string ErrorMessage { get; private set; } = string.Empty;
        public string FileName { get; private set; } = string.Empty;
        public string DataPath { get; set; } = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "\\";
        public bool HasFieldNames { get => fieldNames.Length > 0; }


        /*
         * Constructor loads the data path
         */
        public SmallDataHandler(string path)
        {
            if (path.Length > 0)
            {
                DataPath = path;
            }
        }


        /*
         * Create a file and set it up for read/write
         */
        public bool Create(string fName, int reclen, int fldcount)
        {
            bool Result = false;
            ErrorCode = 0;
            ErrorMessage = string.Empty;

            // Put together file file name and make sure it has an extension
            string ext = Path.GetExtension(fName);
            string UNCName = DataPath + fName;
            if (ext.Length == 0) UNCName += dataExt;
            FileName = string.Empty;

            if (File.Exists(UNCName))
            {
                ErrorCode = 2;
                ErrorMessage = string.Format("File already exists: {0}", UNCName);
            }
            else
            {
                if (reclen > 14)
                {
                    try
                    {
                        recCount = 0;
                        recLen = reclen;
                        fieldCount = fldcount;

                        // Create the file and write the header
                        fs = new FileStream(UNCName, FileMode.Create, FileAccess.ReadWrite);
                        UpdateHeader();
                        FileName = UNCName;
                        Result = true;
                    }
                    catch (Exception ex)
                    {
                        ErrorCode = 5;
                        ErrorMessage = ex.Message;
                    }
                }
                else
                {
                    ErrorCode = 3;
                    ErrorMessage = "Error: Minimum record length is 10 characters.";
                }
            }

            return Result;
        }


        /*
         * Open a file for read/write
         */
        public bool Open(string fName)
        {
            bool Result = false;

            // Put together file file name and make sure it has an extension
            string ext = Path.GetExtension(fName);
            string UNCName = DataPath + fName;
            if (ext.Length == 0) UNCName += dataExt;

            recLen = 0;
            recCount = 0;
            FileName = string.Empty;
            ErrorCode = 0;
            ErrorMessage = string.Empty;

            if (File.Exists(UNCName))
            {
                // open the file
                try
                {
                    fs = new FileStream(UNCName, FileMode.Open, FileAccess.ReadWrite);

                    // read the header
                    byte[] bytes = new byte[15];
                    fs.Read(bytes, 0, 15);
                    string header = Encoding.UTF8.GetString(bytes);

                    // if we can parse the header, check the file length and if ok, we're golden
                    if (int.TryParse(header.Substring(4, 5), out recCount) && int.TryParse(header.Substring(0, 4), out recLen) && int.TryParse(header.Substring(9, 2), out fieldCount))
                    {
                        if (fs.Length > (recCount + 1) * recLen)
                        {
                            // try to fix the fact that the file is longer than expected
                            recCount = (int)fs.Length / recLen - 1;
                        }

                        // Got the rec len bytes and compare to expected
                        if (fs.Length != (recCount + 1) * recLen)
                        {
                            ErrorCode = 20;
                            ErrorMessage = string.Format("Error: File length is {0:N0} and calculated file lenth is {1:N0}", fs.Length, (recCount + 1) * recLen);
                            Result = false;
                            Close();
                        }
                        else
                        {
                            // Get the fieldnames, if any exist
                            bytes = new byte[recLen - 15];
                            fs.Seek(15, SeekOrigin.Begin);
                            fs.Read(bytes, 0, recLen - 15);
                            fieldNames = Encoding.UTF8.GetString(bytes);
                            fieldNames = fieldNames.Replace('\0', ' ').Trim();

                            // Header is parsed, ready to roll
                            FileName = UNCName;
                            Result = true;
                            UpdateHeader();
                        }
                    }
                    else
                    {
                        // calculated file length is wrong and could not be corrected
                        ErrorCode = 7;
                        ErrorMessage = "Error reading header";
                    }
                }
                catch (Exception ex)
                {
                    // Something happened during the open
                    ErrorCode = 6;
                    ErrorMessage = ex.Message;
                }
            }
            else
            {
                // file doesn't existing in the data path
                ErrorCode = 1;
                ErrorMessage = string.Format("File does not exist: {0}", UNCName);
            }

            return Result;
        }

        public void SetDataPath(string path)
        {
            if (path.Length > 0)
            {
                DataPath = path;
            }
        }
 
        /*
         * Convert a table over to a different record length
         * 
         * If the data is longer than the new record length then toss an error.
         * 
         * Create a new file with _NEW after the name, then when done
         * simply rename the file with BAK extension and rename the
         * new file to the file name.
         * 
         * Opens the converted file back up when done.
         * 
         */
        public bool Convert(int newRecLen)
        {
            bool Result = false;
            ErrorCode = 0;
            ErrorMessage = string.Empty;

            SmallDataHandler newFile = new SmallDataHandler(DataPath);
            SmallFileHandlerStructure rec;
            SmallFileHandlerStructure newRec = new SmallFileHandlerStructure(newRecLen, fieldCount);

            // setfieldnames

            try
            {
                // create a new file
                newFile.Create(DataPath + TableName + "_NEW" + dataExt, newRecLen, fieldCount);

                // copy data over
                for (int i = 1; i < recCount; i++)
                {
                    rec = ReadAtIndex(i);

                    if (rec.FieldString.TrimEnd().Length <= newRecLen)
                    {
                        newRec.FieldString = rec.FieldString.PadRight(newRecLen);
                        newFile.AddRec(rec);
                    }
                    else
                    {
                        // we can't let data get truncated when going from
                        // a larger data length table to a shorter length
                        ErrorCode = 92;
                        ErrorMessage = "Data would be truncated";
                        newFile.Close();

                        try
                        {
                            File.Delete(DataPath + TableName + "_NEW" + dataExt);
                        }
                        catch (Exception ex)
                        {
                            ErrorCode = 130;
                            ErrorMessage = "Error 92: Data would be truncated.\r\nError 130: Could not delete " + DataPath + TableName + "_NEW" + dataExt
                                + "\r\nError: " + ex.Message;
                        }
                        break;
                    }
                }

                // close the old file
                newFile.Close();
                Close();

                if (ErrorCode == 0)
                {
                    try
                    {
                        // rename the old file to a BAK file
                        File.Move(DataPath + TableName + dataExt, DataPath + TableName + ".bak");

                        // rename the new file to the correct file name
                        File.Move(DataPath + TableName + "_NEW" + dataExt, DataPath + TableName + dataExt);

                        Result = Open(FileName);

                        ErrorCode = (Result ? 0 : 300);
                        ErrorMessage = (Result ? "Conversion complete" : ErrorMessage);
                    }
                    catch (Exception ex)
                    {
                        ErrorCode = 131;
                        ErrorMessage = "Failed to rename files for conversion of " + TableName + "\r\nError: " + ex.Message;
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorCode = 132;
                ErrorMessage = ex.Message;
            }
            finally
            {
                newFile.Close();
            }

            // open it up
            return Result;
        }


        public bool IsOpen() { return fs != null; }

        /*
         * Close up the file stream, unlock the file, release resources
         */
        public bool Close()
        {
            bool Result = false;

            try
            {
                // try to close it down and release
                // the resources so we can open 
                // another data file
                if (fs != null)
                {
                    fs.Dispose();
                    fs.Close();
                    fs = null;
                }

                // break it up in case there was nothing
                // open, then we just want to make sure
                // the data properties are cleared
                if (fs == null)
                {
                    FileName = string.Empty;
                    recCount = 0;
                    recLen = 0;
                    Result = true;
                }
            }
            catch (Exception ex)
            {
                ErrorCode = 99;
                ErrorMessage = ex.Message;
            }

            return Result;
        }


        /*
         * Remove a deletion mark on a record by index
         * You can recall a not deleted record with no ill effect
         */
        public bool Recall(int recNo)
        {
            try
            {
                SmallFileHandlerStructure recInfo = ReadAtIndex(recNo);
                recInfo.FieldString = "." + recInfo.FieldString.Substring(1);
                SaveRec(recInfo);
            }
            catch (Exception ex)
            {
                ErrorCode = 14;
                ErrorMessage = ex.Message;
            }

            return true;
        }


        /*
         * Put a deletion mark on a record by index
         * You can delete a deleted record with no ill effect
         */
        public bool Delete(int recNo)
        {
            try
            {
                SmallFileHandlerStructure recInfo = ReadAtIndex(recNo);
                recInfo.FieldString = "*" + recInfo.FieldString.Substring(1);
                SaveRec(recInfo);
            }
            catch (Exception ex)
            {
                ErrorCode = 14;
                ErrorMessage = ex.Message;
            }

            return true;
        }


        /*
         * creates a new file with just the records that do not have a 
         * deletion mark.  Old file is renamed into a BAK file.
         */

        public bool Pack()
        {
            bool Result = false;
            ErrorCode = 0;
            ErrorMessage = string.Empty;

            SmallDataHandler newFile = new SmallDataHandler(DataPath);
            SmallFileHandlerStructure rec;
            SmallFileHandlerStructure newRec = new SmallFileHandlerStructure(recLen, fieldCount);
            int rcount = 0;

            try
            {
                // create a new file
                newFile.Create(DataPath + TableName + "_NEW" + dataExt, recLen, fieldCount);

                // copy data over
                for (int i = 1; i < recCount; i++)
                {
                    rec = ReadAtIndex(i);

                    if (rec.Deleted == false)
                    {
                        newRec.FieldString = rec.FieldString.PadRight(recLen);
                        newFile.AddRec(rec);
                        rcount++;
                    }
                }

                // close the old file
                newFile.Close();
                Close();

                try
                {
                    // rename the old file to a BAK file
                    System.IO.File.Move(DataPath + TableName + dataExt, DataPath + TableName + ".bak");

                    // rename the new file to the correct file name
                    System.IO.File.Move(DataPath + TableName + "_NEW" + dataExt, DataPath + TableName + dataExt);

                    Result = Open(FileName);
                    ErrorCode = (Result ? 0 : 300);
                    ErrorMessage = (Result ? string.Format("{0} records copied", rcount) : ErrorMessage);
                }
                catch (Exception ex)
                {
                    ErrorCode = 131;
                    ErrorMessage = "Failed to rename files for conversion of " + TableName
                        + "\r\nError: " + ex.Message;
                }
            }
            catch (Exception ex)
            {
                ErrorCode = 132;
                ErrorMessage = ex.Message;
            }
            finally
            {
                newFile.Close();
            }

            // open it up
            return Result;

        }


        /*
         * Update the header with all of the values - typically we're just
         * updating the record count, but not always.
         */
        private bool UpdateHeader()
        {
            bool Result = false;

            if (fs != null)
            {
                try
                {
                    string headerRec = recLen.ToString("D4") + recCount.ToString("D5") + fieldCount.ToString("D2");

                    if (fs.Length < recLen)
                        headerRec = headerRec.PadRight(recLen); // If called by Create, we need to pad it
                    else
                        headerRec = headerRec.PadRight(15);

                    byte[] bytes = Encoding.UTF8.GetBytes(headerRec);
                    fs.Seek(0, SeekOrigin.Begin);
                    fs.Write(bytes, 0, bytes.Length);
                    Result = true;
                }
                catch (Exception ex)
                {
                    ErrorCode = 11;
                    ErrorMessage = ex.Message;
                }
            }

            return Result;
        }


        public string[] GetFieldNamesArray()
        {
            return (fieldNames.Length > 0 ? fieldNames.Split(',') : null);
        }

        /*
         * Update the header with the list of field names 
         * provided as a comma seperated list
         */
        public bool SetFieldNames(string fieldlist)
        {
            bool Result = false;

            // make sure it will fit
            if (fieldlist.Length + 15 < recLen)
            {
                // if there is only one field or there are commas 
                // in the field list then save it
                if (fieldlist.Contains(",") || fieldCount == 1)
                {
                    byte[] bytes = Encoding.UTF8.GetBytes(fieldlist);
                    fs.Seek(15, SeekOrigin.Begin);
                    fs.Write(bytes, 0, bytes.Length);
                    fieldNames = fieldlist;
                    Result = true;
                }
                else
                {
                    ErrorCode = 300;
                    ErrorMessage = "List of field names must contain at least two names delimited by commas";
                }
            }
            else
            {
                ErrorCode = 300;
                ErrorMessage = string.Format("Record length of {0} is too short to hold field name list", recLen);
            }
            return Result;
        }

        public int GetFieldIdx(string fn)
        {
            SmallFileHandlerStructure blank = BlankRec();
            return blank.GetFieldIdx(fn);
        }


        /*
         * Create a blank record
         */
        public SmallFileHandlerStructure BlankRec()
        {
            SmallFileHandlerStructure recInfo = new SmallFileHandlerStructure(recLen, fieldCount);

            // if the reclen is minimally long enough
            // then set up the field count
            string r = "." + "".PadRight(fieldCount + 1, '|');
            recInfo.FieldString = r.PadRight(recLen);
            recInfo.RecNo = 0;
            recInfo.EOF = false;
            recInfo.SetFieldNames(fieldNames.Split(','));
            return recInfo;
        }


        /*
         * Add a record to the end of the file
         * Returns the SFH structure updated with the current record location
         *
         */
        public SmallFileHandlerStructure AddRec(SmallFileHandlerStructure recInfo)
        {
            recCount++;

            byte[] bytes = Encoding.UTF8.GetBytes(recInfo.FieldString);
            fs.Seek(0, SeekOrigin.End);
            fs.Write(bytes, 0, recLen);
            recInfo.RecNo = recCount;
            recInfo.EOF = true;
            UpdateHeader();

            return recInfo;
        }

        /*
         * Save a record at the 
         */
        public SmallFileHandlerStructure SaveRec(SmallFileHandlerStructure recInfo)
        {
            if (fs != null)
            {
                if (recInfo.RecNo > 0)
                {
                    if (recInfo.RecNo > recCount)
                    {
                        ErrorCode = 11;
                        ErrorMessage = "Cannot write past the end of the file.  Use the AddRec method to append a record.";
                    }
                    else
                    {
                        try
                        {
                            byte[] bytes = Encoding.UTF8.GetBytes(recInfo.FieldString);
                            fs.Seek((long)(recLen * recInfo.RecNo), SeekOrigin.Begin);
                            fs.Write(bytes, 0, recLen);
                        }
                        catch (Exception ex)
                        {
                            ErrorCode = 10;
                            ErrorMessage = ex.Message;
                        }
                    }
                }
                else
                {
                    ErrorCode = 11;
                    ErrorMessage = "Cannot write before the first record (RecNo < 1)";
                }
            }

            return recInfo;
        }

        /*
         * Read in a record based on record number
         * 
         */
        public SmallFileHandlerStructure ReadAtIndex(int recNo)
        {
            SmallFileHandlerStructure recInfo = new SmallFileHandlerStructure(recLen, fieldCount);
            recInfo.SetFieldNames(fieldNames.Split(','));

            if (fs != null)
            {
                recInfo = BlankRec();
                recInfo.EOF = (recNo > 0);

                try
                {
                    // don't try to read past end of file
                    if (recCount > 0 && recCount >= recNo)
                    {
                        fs.Seek((long)recLen * recNo, SeekOrigin.Begin);
                        byte[] bytes = new byte[recLen];
                        fs.Read(bytes, 0, recLen);

                        string r = Encoding.UTF8.GetString(bytes);
                        recInfo.FieldString = r.Replace('\0', ' ');
                        recInfo.EOF = (recNo >= recCount);
                        recInfo.RecNo = recNo;
                    }
                }
                catch (Exception ex)
                {
                    // Return a blank record on an error
                    ErrorCode = 10;
                    ErrorMessage = ex.Message;
                }
            }
            return recInfo;
        }

        public SmallFileHandlerStructure FindRec(int fieldNo, string match, Match matchType)
        {
            SmallFileHandlerStructure recInfo = BlankRec();

            int fNo = fieldNo; // field numbers are 0 based
            bool matched = false;

            for (int i = 1; i <= recCount; i++)
            {
                recInfo = ReadAtIndex(i);

                if (fNo < recInfo.Fields.Length)
                {
                    switch (matchType)
                    {
                        case Match.Exact:
                            matched = (recInfo.Fields[fNo].Equals(match));
                            break;

                        case Match.ExactCaseInsensitive:
                            matched = (recInfo.Fields[fNo].Equals(match, StringComparison.OrdinalIgnoreCase)); // ignore case
                            break;

                        case Match.PartialCaseInsensitive:
                            matched = (recInfo.Fields[fNo].ToUpper().Contains(match.ToUpper())); // partial and ignore case
                            break;
                    }

                    if (matched) break;
                }
            }

            return (matched ? recInfo : BlankRec());
        }
    }

    /*=====================================================================
     * Structure used to handle a record
     */
    public class SmallFileHandlerStructure
    {
        private int recLen;
        private int fldCount;
        private string[] fieldNames;
        public bool HasFieldNames { get => fieldNames.Length > 0; }

        public SmallFileHandlerStructure(int rl, int fc)
        {
            recLen = rl;
            fldCount = fc;
            RecNo = 0;
            EOF = false;

            string r = "".PadRight(fc, '|');
            FieldString = ("." + r).PadRight(recLen);
        }

        // Set up teh field names
        public void SetFieldNames(string[] fn)
        {
            fieldNames = fn;
            for (int i = 0; i < fieldNames.Length; i++) fieldNames[i] = fieldNames[i].ToUpper();
        }

        // get field index by name
        public int GetFieldIdx(string fn) { return Array.IndexOf(fieldNames, fn.ToUpper()); }

        // Get a Field[] based on the field name
        public string GetField(string fn)
        {
            string Result = string.Empty;

            if (fieldNames != null)
            {
                int i = Array.IndexOf(fieldNames, fn.ToUpper());
                Result = (i >= 0 ? (i < Fields.Length ? Fields[i] : "") : null);
            }

            return Result;
        }

        public string[] GetFieldNamesArray()
        {
            return (fieldNames.Length > 0 ? fieldNames : null);
        }


        // Save to a Field[] based on the field name
        public bool SetField(string fn, string value)
        {
            int i = Array.IndexOf(fieldNames, fn.ToUpper());
            if (i >= 0 && i < Fields.Length) Fields[i] = value;
            return i >= 0 && i < Fields.Length;
        }

        public bool Deleted { get; private set; } = false;
        public int RecNo { get; set; }
        public bool EOF { get; set; }
        public string[] Fields { get; set; }

        public void Delete() { Deleted = true; }
        public void Recall() { Deleted = false; }

        public string FieldString
        {
            // Pull data from the fields and make the data string
            get
            {
                string r = string.Empty;

                for (int i = 0; i < Fields.Length; i++)
                {
                    if (Fields[i] == null)
                        r += "$NULL$|";
                    else
                        r += Fields[i] + "|";
                }

                if (Fields.Length < fldCount) r += "".PadRight(fldCount - Fields.Length, '|');  // make sure we have the expected number of fields

                r = (Deleted ? "*" : ".") + r; // Prepend deletion mark
                r = r.PadRight(recLen, ' '); // pad spaces to RecLen

                if (r.Length > recLen) throw new ApplicationException("Data would be truncated");
                return r;
            }

            // take a data string and drop it into the fields
            set
            {
                string r = value;

                Deleted = (r.Substring(0, 1).Equals("*")); // set the deleted value
                r = r.Substring(1).TrimEnd();  // kill the trailing spaces

                int freq = Regex.Matches(r, "|").Count;
                if (freq < fldCount) r = r.PadRight(fldCount - freq, '|'); // Make sure we have enough field markers
                r = r.Substring(0, r.Length - 1); // Trim off the trailing |
                Fields = r.Split('|'); // Break it into the Fields[]

                for (int i = 0; i < Fields.Length; i++)
                {
                    if (Fields[i].Equals("$NULL$"))
                        Fields[i] = null;
                }
            }
        }
    }
}