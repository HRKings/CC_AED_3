using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading;
using System.Xml;

namespace InvertedList
{
    public class InvertedListImplementation
    {
        // The term table
        private Dictionary<string, long> _terms;

        // Names table
        private Dictionary<int, string> _names;

        // The path of the terms table file
        private string TermsPath { get; set; }

        // The path to the IDs blocks file
        private string IDsPath { get; set; }

        // The path to the names file
        private string NamesFile { get; set; }

        public InvertedListImplementation(string termsPath, string idsPath, string namesFile)
        {
            TermsPath = termsPath;
            IDsPath = idsPath;
            NamesFile = namesFile;

            try
            {
                // Tries to open the terms table from a file
                _terms = OpenTable<Dictionary<string, long>>(termsPath);
            }
            catch
            {
                // If fails to open from the file, then creates an empty table
                _terms = new Dictionary<string, long>();
            }

            try
            {
                // Tries to open the name table from a file
                _names = OpenTable<Dictionary<int, string>>(namesFile);
            }
            catch
            {
                // If fails to open from the file, then creates an empty table
                _names = new Dictionary<int, string>();
            }
        }

        /// <summary>
        /// Writes the term table into a file
        /// </summary>
        /// <param name="path">The path where to store the file</param>
        private void WriteTable(string path, object table)
        {
            // Opens a memory stream to hold the conversion
            using (MemoryStream mStream = new MemoryStream())
            {
                // Creates a binary formatter to convert from dictionary to bytes
                BinaryFormatter binFormatter = new BinaryFormatter();

                // Converts the term table to binary and store in the memory stream
                binFormatter.Serialize(mStream, table);

                // Writes the bytes on the file
                File.WriteAllBytes(path, mStream.ToArray());
                mStream.Close();
            }
        }

        /// <summary>
        /// Opens a file containing the terms table
        /// </summary>
        /// <param name="path">The path of the terms table</param>
        /// <returns>A dictionary containing the terms and addresses</returns>
        private T OpenTable<T>(string path)
        {
            // Opens the file and stores the bytes into an array
            byte[] file = File.ReadAllBytes(path);

            // Creates a new memory stream to hold the conversion
            using (MemoryStream mStream = new MemoryStream())
            {
                // Creates a binary formatter to convert from byte to dictionary
                BinaryFormatter binFormatter = new BinaryFormatter();

                // Write all the bytes from the file byte array into the memory stream then resets the position back to the start
                mStream.Write(file, 0, file.Length);
                mStream.Position = 0;

                // Converts the memory stream from bytes to dictionary then returns it
                T temp = ((T)binFormatter.Deserialize(mStream));
                mStream.Close();

                return (temp);
            }
        }

        /// <summary>
        /// Gets a name from the name table
        /// </summary>
        /// <param name="id">The ID to search for</param>
        /// <returns>A string containing the name or prints Invalid ID on the console</returns>
        public string QueryID(int id)
        {
            if (_names != null)
            {
                return _names.GetValueOrDefault(id, "Invalid ID");
            }

            return null;
        }

        /// <summary>
        /// Creates a new name adding it to the ids table and the terms table
        /// </summary>
        /// <param name="id">The ID of the name</param>
        /// <param name="name">The name to add</param>
        public void Create(int id, string name)
        {
            // Checks to see if we have a terms and names table
            if (_terms != null && _names != null)
            {
                // Checks to see if we already have added this ID to the database
                if (_names.ContainsKey(id))
                {
                    // Writes on the console that the ID is already on the database
                    Console.WriteLine("The database already contains this ID");
                    return;
                }
                else
                {
                    // If we haven't added the id on the database, then adds it on the name table
                    _names.Add(id, name);

                    // Writes name table onto disk
                    WriteTable(NamesFile, _names);
                }

                // Loops trough the normalized words
                foreach (string word in Program.Normalize(name).Split(' '))
                {
                    // If the word isn't invalid, procceds
                    if (!String.IsNullOrWhiteSpace(word))
                    {
                        // Tries to see if the term is on the table
                        long address = _terms.GetValueOrDefault(word, -1);

                        // If the term doesn't exist, then add it on the table
                        if (address == -1)
                        {
                            long endOfFile = 0;
                            if (File.Exists(IDsPath))
                            {
                                // Gets the end of the file
                                FileInfo temp = new FileInfo(IDsPath);
                                endOfFile = temp.Length;
                            }

                            // Adds term on the table, with the address of the end of the file
                            _terms.Add(word, endOfFile);

                            // Writes table into the file
                            WriteTable(TermsPath, _terms);

                            // Creates the registry on the IDs list
                            CreateRegistry(endOfFile, id, IDsPath);
                        }
                        // If the term exist, then update the list with the new ID
                        else
                        {
                            // Calls the update function to write the new ID
                            WriteRegistry(address, id, IDsPath);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Creates a new list of IDs
        /// </summary>
        /// <param name="address">Where to create the list (preferably at the end of the file)</param>
        /// <param name="firstValue">The first ID of the list</param>
        /// <param name="path">The path where the list is stored</param>
        private void CreateRegistry(long address, int firstValue, string path)
        {
            // Opens the file
            using (Stream stream = new FileStream(path, FileMode.OpenOrCreate))
            {
                // Creates an array with the first ID and -1 on the other 9 positions
                int[] values = new int[] { firstValue, -1, -1, -1, -1, -1, -1, -1, -1, -1 };

                // Initializes an empty byte array
                byte[] valueArray = null;

                // Seek the address of the list
                stream.Seek(address, SeekOrigin.Begin);

                // Adds each value of the list on the file
                foreach (int val in values)
                {
                    // Convert the value to byte array
                    valueArray = BitConverter.GetBytes(val);

                    // Writes the byte array into the file
                    stream.Write(valueArray, 0, valueArray.Length);
                    valueArray = null;
                }

                valueArray = null;
                // Converts -1 to byte array, corresponding to the pointer to the next block, which doesn't exist
                valueArray = BitConverter.GetBytes((long)-1);

                // Writes the next block pointer to the file
                stream.Write(valueArray, 0, valueArray.Length);
                stream.Close();
            }
        }

        /// <summary>
        /// Modify a registry adding a new value to it or, if full, creates a new block at end of the file
        /// </summary>
        /// <param name="address">The adress of the list</param>
        /// <param name="value">The value to add to the block</param>
        /// <param name="path">The path where the file is</param>
        private void WriteRegistry(long address, int value, string path)
        {
            // Initializes an array with 10 positions
            int[] values = new int[10];

            // Sets the next block pointer to -1 (non existant)
            long nextBlock = -1;

            // Records the end of the file
            long lastBlock = 0;

            // Opens the file for reading
            using (FileStream file = new FileStream(path, FileMode.Open))
            {
                // Opens a binary converter to retrieve the ints from the bytes
                using (BinaryReader binaryStream = new BinaryReader(file))
                {
                    // Calls the get block method to get the values present on the block and its address
                    GetBlock(file, binaryStream, ref address, ref values);

                    // Gets the end of the file
                    lastBlock = file.Length;
                }
                file.Close();
            }

            // Creates an index variable to keep track of available space on the list
            int index = 0;

            // Iterates from 0 to 10 possible values
            for (index = 0; index < 10; index++)
            {
                // If we encounter an empty position (marked as -1) then we insert the new ID on it
                if (values[index] == -1)
                {
                    // The -1 turns into the ID value
                    values[index] = value;

                    // Sets the index as 200 to serve as a code and to break the loop
                    index = 200;
                }
            }

            // If we don't encounter the index 200 (success code) then the block is full and we need to create a new one
            if (index != 200)
            {
                // Creates a new list on the end of the file
                CreateRegistry(lastBlock, value, path);

                // Sets the nextblock to the end of the file
                nextBlock = lastBlock;
            }

            // Opens the IDs file
            using (Stream stream = new FileStream(path, FileMode.OpenOrCreate))
            {
                // Creates a placeholder byte array
                byte[] valueArray = null;

                // Seeks the address of the current block
                stream.Seek(address, SeekOrigin.Begin);

                // Iterates the values of the current list
                foreach (int val in values)
                {
                    // Converts the value to byte array
                    valueArray = BitConverter.GetBytes(val);

                    // Writes the array to the file
                    stream.Write(valueArray, 0, valueArray.Length);
                    valueArray = null;
                }

                // Converts the next block address (-1 or the end of the file, where the newly created array is) to byte array
                valueArray = BitConverter.GetBytes(nextBlock);

                // Writes the byte array to the file
                stream.Write(valueArray, 0, valueArray.Length);
                stream.Close();
            }
        }

        /// <summary>
        /// Gets all values on the last block containing the word and updates the value
        /// </summary>
        /// <param name="file">The file stream to use when retrieving the values</param>
        /// <param name="binaryStream">The binary stream to convert the byte array to int</param>
        /// <param name="address">The address of the block to start the search, passed by reference to bring back the address of the last on the chain</param>
        /// <param name="values">An array holding the values of the last block on the chain, passed by reference to avoid return statments</param>
        private void GetBlock(FileStream file, BinaryReader binaryStream, ref long address, ref int[] values)
        {
            // Seeks the address of the list
            file.Seek(address, SeekOrigin.Begin);

            // Iterate trough the 10 possible values
            for (int i = 0; i < 10; i++)
            {
                // Creates a ID placeholder with -1 (empty)
                int read = -1;

                try
                {
                    // Reads the int from the file
                    read = binaryStream.ReadInt32();
                }
                catch
                {
                    // If fails fallback to -1
                    read = -1;
                }

                // Sets the value on the array
                values[i] = read;
            }

            // Gets the next block address
            long nextBlock = binaryStream.ReadInt64();

            // If the next block address isn't empty (-1) then call this function again with the next block on the chain
            if (nextBlock != -1)
            {
                // Sets the address refernce to the next block address
                address = nextBlock;

                // Calls the function again with the address of the next block
                GetBlock(file, binaryStream, ref address, ref values);
            }
        }

        /// <summary>
        /// Makes a query based on the terms used
        /// </summary>
        /// <param name="query">The array of strings containing the words to query from</param>
        /// <returns>An array of the IDs that contains the queried words</returns>
        public int[] Read(string[] query)
        {
            // Tests to see if the terms table exists
            if (_terms == null)
            {
                // Debugs if the table dont exist
                Console.WriteLine("The terms table is empty");
            }
            // If the terms table exists, proceeds
            else
            {
                // Creates an empty list to hold the results
                List<int> result = new List<int>();

                // Loops the queries terms
                for (int index = 0; index < query.Length; index++)
                {
                    // Tries to get the address of the term from the table or returns -1 if it isn't on the table
                    long address = _terms.GetValueOrDefault(query[index], -1);

                    // If the address exists (it's not equal to -1) the procceeds
                    if (address != -1)
                    {
                        if (index == 0)
                        {
                            // If we are comparing the first term, then the result list will be equal to the list from the file
                            result = Retrieve(address, IDsPath);
                        }
                        else
                        {
                            List<int> nextList = Retrieve(address, IDsPath);
                            // If it isn't the first term, then we intersects the result from the second term to narrow the results
                            result = result.Intersect(nextList).ToList();
                        }
                    }
                }

                // Returns the results in array form
                return result.ToArray();
            }

            // Returns null if something fails
            return null;
        }

        /// <summary>
        /// Restrieves the ID list from the secondary file
        /// </summary>
        /// <param name="address"> The address where the ID list is stored</param>
        /// <param name="path">The path of the secondary file</param>
        /// <returns>A list containing all the IDs of the address and of its pointers</returns>
        private List<int> Retrieve(long address, string path)
        {
            // Initializes an empty list
            List<int> result = new List<int>();

            // Opens the file for reading
            using (FileStream file = new FileStream(path, FileMode.Open))
            {
                // Opens a binary converter to retrieve the ints from the bytes
                using (BinaryReader binaryStream = new BinaryReader(file))
                {
                    // Calls the retrieve function backend to retrieve the address
                    _retrieve(binaryStream, file, address, ref result);
                }
                file.Close();
            }

            // Returns the result int list
            return result;
        }

        /// <summary>
        /// The backend of the retrieve function, that can retrieve multiple addresses recursively if needed to without reopening the file
        /// </summary>
        /// <param name="binaryStream">The binary converter to retrieve the ints</param>
        /// <param name="file">The file stream to retrieve from</param>
        /// <param name="address">The address to start reading</param>
        /// <param name="result">The result list reference to make this function use only one list and don't require a return statement</param>
        private void _retrieve(BinaryReader binaryStream, FileStream file, long address, ref List<int> result)
        {
            // Seeks the address of the list
            file.Seek(address, SeekOrigin.Begin);

            // Reads all the possible values of the list, which is a maximum of 10 ids per block
            for (int i = 0; i < 10; i++)
            {
                int read = -1;
                try
                {
                    // Reads the int from the file
                    read = binaryStream.ReadInt32();
                }
                catch
                {
                    read = -1;
                }

                // Tests if the readed int is -1 (the empty)
                if (read != -1)
                {
                    // If the int it's not empty then checks to see if the ID is already on the list
                    if (!result.Contains(read))
                    {
                        // If its not on the list, then add it
                        result.Add(read);
                    }
                }
            }

            // After reading the 10 possible values of the block, tries to read the pointer to the next block
            long nextBlock = binaryStream.ReadInt64();

            // Tests to see if the next block exists (the address is different of -1)
            if (nextBlock != -1)
            {
                // If the next block exists, then call this function again but starting from the next block
                _retrieve(binaryStream, file, nextBlock, ref result);
            }
        }
    }
}