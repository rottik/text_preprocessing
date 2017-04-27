using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Linq;
using System.Diagnostics;
using System.Xml;
using NHunspell;

namespace Preprocessing
{
    class Program
    {
        static void Main(string[] args)
        {
            //Preprocessing prep = new Preprocessing("data", Preprocessing.Lemmatizer.Hunspell, Preprocessing.Language.cs);
            Preprocessing prep = new Preprocessing(new FileInfo("Preprocessing.cs.txt"));

            foreach (string file in Directory.GetFiles(@"D:\summarizace\audiodata", "*.clause", SearchOption.AllDirectories))
            {
                File.WriteAllLines(file + "Long.txt", prep.Clauses2Sentences(File.ReadAllLines(file), false));
            }

            //string[] lines = prep.Clauses2Sentences(File.ReadAllLines("text.txt"), false);



            //string[] lines = prep.Raw2sents();

            //Sentence[] s = prep.GetLemma(lines, 0);

            Console.WriteLine();
        }
    }


    public class Preprocessing
    {
        Dictionary<string, double> idf = new Dictionary<string, double>();
        Dictionary<string, HashSet<string>> theraurus;
        HashSet<string> stoplist = new HashSet<string>();
        double medianIDF = 0;
        FileInfo morphoditeDataFile, morphoditeExetutableFile, synonymsFile, stopFile, idfFile, abbrevationsFile, majkaExe, majkaDataFile, pythonFile;
        DirectoryInfo majkaDataDir;
        Hunspell speller = null;
        List<string> podradiciSpojky = new List<string>("zatímco, aby, ač, ačkoli, ačkoliv, aniž, ať, ať již, ať už, až, byť, co, dokud, i, jak, jakkoli, jakkoliv, jakmile, jako, jakož, jelikož, jen co, jestli, jestliže, kdežto, kdyby, kdykoli, kdykoliv, když, leda, ledaže, ledva, ledvaže, mezitímco, nechť, než, nežli, pakli, pakliže, pokud, poněvadž, potéco, protože, přestože, sotva, sotvaže, takže, třeba, třebas, třebaže, zatímco, zda, zdali, že".Split(new string[] { "," }, StringSplitOptions.RemoveEmptyEntries).Select(p => p.Trim()));
        string[] zkratky;
        private Encoding win = Encoding.GetEncoding(1250);
        private Encoding iso = Encoding.GetEncoding(28592);
        string path2data = ".";
        Process p;

        private Lemmatizer engine = Lemmatizer.Hunspell;
        private Language lang = Language.cs;

        /// <summary>
        /// The Property gets the Stoplist of current instance of the preproecssing engine.
        /// </summary>
        public HashSet<string> Stoplist
        {
            get { return stoplist; }
        }

        /// <summary>
        /// The MedianIDF property gets the value of inverse document frequency of median word in engines language.
        /// </summary>
        public double MedianIDF
        {
            get
            {
                if (medianIDF != 0) return medianIDF;
                else
                {
                    medianIDF = idf.Values.OrderBy(p => p).ToArray()[idf.Count / 2];
                    return medianIDF;
                }
            }
        }

        public Dictionary<string, double> IDF
        {
            get { return idf; }
        }

        /// <summary>
        /// Enumerator of supported morphological analysers
        /// </summary>
        public enum Lemmatizer { Hunspell, FST, FMorph, MorphoDiTa, Majka, dummy };

        /// <summary>
        /// Enumerator of supported languages for Hunspell
        /// </summary>
        public enum Language { cs, pl, sk, ru, hr, sl, sr };

        /// <summary>
        /// 
        /// </summary>
        public Language Lang
        {
            get { return lang; }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="configFile"></param>
        public Preprocessing(FileInfo configFile)
        {
            if (!configFile.Exists)
                throw new FileNotFoundException("File " + configFile.FullName + " doesn't exists.");
            else
            {
                Console.WriteLine("Preprocessing configuration file:" + configFile.FullName);
                Console.WriteLine(File.ReadAllText(configFile.FullName));
            }

            string[] lines = File.ReadAllLines(configFile.FullName);
            //engine=XXX
            //language=XXX
            //dataDir=listsDir
            //morphoditeExe=pathOfExe
            //morphoditeData=pathOfData

            foreach (string line in lines)
            {
                if (line.Contains("engine"))
                {
                    string en = line.Split('=')[1].ToLower().Trim();
                    foreach (Lemmatizer value in Enum.GetValues(typeof(Lemmatizer)))
                    {
                        if (en == value.ToString().ToLower())
                            engine = value;
                    }
                }
                if (line.Contains("language"))
                {
                    string lg = line.Split('=')[1].ToLower().Trim();
                    foreach (Language value in Enum.GetValues(typeof(Language)))
                    {
                        if (lg == value.ToString().ToLower())
                            lang = value;
                    }
                }
                if (line.Contains("dataDir"))
                {
                    path2data = line.Split('=')[1].Trim();
                }
                if (line.Contains("morphoditeExe"))
                {
                    morphoditeExetutableFile = new FileInfo(line.Split('=')[1].Trim());
                    if (!morphoditeExetutableFile.Exists)
                        throw new FileNotFoundException("File " + morphoditeExetutableFile.FullName + " doesn't exists.");
                    else
                        Console.WriteLine("Morphodite binary " + morphoditeExetutableFile.FullName);
                }

                if (line.Contains("python"))
                {
                    pythonFile = new FileInfo(line.Split('=')[1].Trim());
                    if (!pythonFile.Exists)
                        throw new FileNotFoundException("File " + pythonFile.FullName + " doesn't exists.");
                    else
                        Console.WriteLine("python binary " + pythonFile.FullName);
                }
                if (line.Contains("morphoditeData"))
                {
                    morphoditeDataFile = new FileInfo(line.Split('=')[1].Trim());
                    if (!morphoditeDataFile.Exists)
                        throw new FileNotFoundException("File " + morphoditeDataFile.FullName + " doesn't exists.");
                    else
                        Console.WriteLine("Morphodite data " + morphoditeDataFile.FullName);
                }
                if (line.Contains("majkaExe"))
                {
                    majkaExe = new FileInfo(line.Split('=')[1].Trim());
                    if (!majkaExe.Exists)
                        throw new FileNotFoundException("File " + majkaExe.FullName + " doesn't exists.");
                    else
                        Console.WriteLine("Majka binary " + majkaExe.FullName);
                }
                if (line.Contains("majkaData"))
                {
                    majkaDataDir = new DirectoryInfo(line.Split('=')[1].Trim());
                    if (!majkaDataDir.Exists)
                        throw new FileNotFoundException("File " + majkaDataDir.FullName + " doesn't exists.");
                    else
                        Console.WriteLine("Majka data " + majkaDataDir.FullName);
                }
            }

            LoadData(path2data);
        }

        private void LoadData(string path2DataDir)
        {
            synonymsFile = new FileInfo(path2DataDir + "/synonyms." + lang);
            if (!synonymsFile.Exists)
                throw new FileNotFoundException(synonymsFile.FullName + " not found!");

            idfFile = new FileInfo(path2DataDir + "/idf." + lang);
            if (!idfFile.Exists)
                throw new FileNotFoundException(idfFile.FullName + " not found!");

            stopFile = new FileInfo(path2DataDir + "/stopwords." + lang);
            if (!stopFile.Exists)
                throw new FileNotFoundException(stopFile.FullName + " not found!");

            abbrevationsFile = new FileInfo(path2DataDir + "/abbrevations." + lang);
            if (!abbrevationsFile.Exists)
                throw new FileNotFoundException(abbrevationsFile.FullName + " not found!");

            zkratky = File.ReadAllLines(abbrevationsFile.FullName);

            theraurus = ReadThesaurus(synonymsFile.FullName);

            string[] lines = File.ReadAllLines(idfFile.FullName);
            double pocetClanku = Convert.ToDouble(lines[0].Substring(lines[0].IndexOf(":") + 1));
            int cnt = 0;
            foreach (string line in lines)
            {
                if (line.Contains(":")) continue;
                string[] parts = line.Split(new string[] { "\t" }, StringSplitOptions.RemoveEmptyEntries);
                double occ = Convert.ToDouble(parts[1]);
                idf.Add(parts[0], Math.Log(pocetClanku / occ));
                if (cnt == (lines.Length - 1) / 2)
                    medianIDF = Math.Log(pocetClanku / occ);
            }

            //TODO: upravit cesty
            lines = File.ReadAllLines(stopFile.FullName);
            foreach (string word in lines)
                stoplist.Add(word);
            Console.WriteLine("preprocessing loaded");
        }

        /// <summary>
        /// Ctor create instance of the preprocessing class and loads the Thesaurus, The Stoplist and the IDF list from engine's directory. 
        /// Example of path: ".\\Hunspell\\idf.cs"
        /// </summary>
        /// <param name="path2DataDir"></param>
        /// <param name="engine"></param>
        /// <param name="language"></param>
        public Preprocessing(string path2DataDir, Lemmatizer engine, Language language)
        {
            DirectoryInfo di = new DirectoryInfo(path2DataDir);
            if (di.Exists)
                path2data = path2DataDir;
            else
                throw new DirectoryNotFoundException("Ditectory not found.\n" + di.FullName);

            if (language == Language.cs)
                this.engine = engine;
            else
                this.engine = Lemmatizer.Hunspell;

            if (engine == Lemmatizer.Hunspell)
                this.lang = language;
            else if (language == Language.cs)
                this.lang = Language.cs;
            else
                throw new Exception("Only Czech language is supported by selected lemmatizer!");

            LoadData(path2data);
        }

        /// <summary>
        /// Method loads synomyms from file. In format: repezentant\tword1, word2, word3
        /// </summary>
        /// <param name="synonymFile"></param>
        /// <returns></returns>
        private Dictionary<string, HashSet<string>> ReadThesaurus(string synonymFile)
        {
            Dictionary<string, HashSet<string>> theraurus = new Dictionary<string, HashSet<string>>();
            // TODO: zmenit cesty
            TextReader tr = new StreamReader(synonymFile, Encoding.UTF8);
            string line = null;
            List<string> allwords = new List<string>();
            while ((line = tr.ReadLine()) != null)
            {
                HashSet<string> tmp = new HashSet<string>();
                string repre = line.Substring(0, line.IndexOf("-")).Trim();
                foreach (string slovo in line.Substring(line.IndexOf("-") + 1).Split(new string[] { ",", " " }, StringSplitOptions.RemoveEmptyEntries).Where(p => !p.Contains(" ")).Select(p => p.ToLower()))
                {
                    if (!tmp.Contains(slovo))
                        tmp.Add(slovo);
                    if (allwords.Contains(slovo))
                    {
                        foreach (string key in theraurus.Keys)
                        {
                            if (theraurus[key].Contains(slovo))
                            {
                                repre = key;
                                break;
                            }
                        }
                    }
                }

                if (!theraurus.ContainsKey(repre))
                    theraurus.Add(repre, tmp);
                else
                    theraurus[repre].Union(tmp);
                allwords = allwords.Union(theraurus[repre]).ToList();
            }
            tr.Close();
            return theraurus;
        }

        /// <summary>
        /// Method returns lemmatized input sentences.
        /// </summary>
        /// <param name="sents">Input sentences</param>
        /// <param name="id">Uniq ID for multitasking.</param>
        /// <returns></returns>
        public Sentence[] GetLemma(string[] sents, int id)
        {
            #region hunspell
            if (engine == Lemmatizer.Hunspell)
            {
                string baseDicsPath = @"./Hunspell/slovniky/";
                string csAffFile = baseDicsPath + lang + ".aff";
                string csDictFile = baseDicsPath + lang + ".dic";
                if (speller == null)
                    speller = new Hunspell(csAffFile, csDictFile);
                List<Sentence> lemmatizedSentences = new List<Sentence>();

                foreach (string sent in sents)
                {
                    StringBuilder sb = new StringBuilder();
                    string[] words = Regex.Split(sent, @"[\p{M}\p{P}\s]", RegexOptions.Compiled);
                    foreach (string word in words)
                        if (word != null)
                            sb.Append(LemmatizeWordWithHunspell(word, speller) + " ");
                    lemmatizedSentences.Add(new Sentence(sb.ToString().Trim(), sent));
                }
                return lemmatizedSentences.ToArray();
            }
            #endregion

            #region majka
            if (engine == Lemmatizer.Majka)
            {
                File.WriteAllLines("text.txt", sents);

                string unitokFile = "majka/majkalemmatize.py";

                FileInfo majkascript = new FileInfo(unitokFile);

                ProcessStartInfo si = new ProcessStartInfo(pythonFile.FullName);
                si.Arguments = majkascript.FullName;
                si.UseShellExecute = true;

                p = new Process();
                p.StartInfo = si;
                p.Start();

                File.ReadAllLines("lemma.txt");
            }
            #endregion

            #region FST
            if (engine == Lemmatizer.FST)
                throw new NotImplementedException("FST lemmatization is not implemented yet.");
            #endregion

            #region FMorph
            if (engine == Lemmatizer.FMorph)
            {
                string[] vetyS = LemmatizeSentenciesWithFMorph(sents.ToArray(), id);

                Sentence[] output = new Sentence[vetyS.Length];
                if (vetyS.Length == sents.Length)
                    for (int i = 0; i < vetyS.Length; i++)
                        output[i] = new Sentence(vetyS[i], sents[i]);
                else
                    throw new Exception("Chyba: ruzny pocet vet po lemmatizaci");

                return output;
            }
            #endregion

            #region morphodita
            if (engine == Lemmatizer.MorphoDiTa)
            {
                StringBuilder sb = new StringBuilder();
                foreach (string s in sents)
                    sb.AppendLine(s.Replace("\n", " ").Replace("\r", " ") + "  ");
                string text = sb.ToString();

                //File.WriteAllText("tmp.txt", sb.ToString());

                ProcessStartInfo si = new ProcessStartInfo(morphoditeExetutableFile.FullName);
                si.Arguments = "--convert_tagset=strip_lemma_comment \"" + morphoditeDataFile.FullName + "\"";
                si.UseShellExecute = false;
                si.RedirectStandardInput = true;
                si.RedirectStandardOutput = true;

                p = new Process();
                p.StartInfo = si;
                p.Start();

                StreamWriter sw = p.StandardInput;
                //string text = sw.Encoding.GetString(Encoding.UTF8.GetBytes(sb.ToString()));
                byte[] writeBuffer = Encoding.UTF8.GetBytes(text);
                sw.BaseStream.Write(writeBuffer, 0, writeBuffer.Length);
                sw.Close();

                StreamReader sr = p.StandardOutput;
                string messedupText = sr.ReadToEnd();
                string rawXml = Encoding.UTF8.GetString(sr.CurrentEncoding.GetBytes(messedupText));

                /*
                byte[] buffer = new byte[1024];
                StringBuilder outcome = new StringBuilder();
                int bytesRead = 0;
                while ((bytesRead = sr.BaseStream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    outcome.Append(Encoding.UTF8.GetString(buffer));
                }
                sr.Close();
                */
                //string rawXml = outcome.ToString();

                XmlDocument doc = new XmlDocument();
                doc.LoadXml("<article>" + rawXml + "</article>");
                XmlNodeList sentences = doc.SelectNodes("//sentence");
                sb.Clear();

                StringBuilder sb2 = new StringBuilder();
                foreach (XmlNode sentence in sentences)
                {
                    foreach (XmlNode token in sentence.SelectNodes("token"))
                    {
                        sb.Append(token.Attributes["lemma"].InnerText + " ");
                        sb2.Append(token.InnerText + " ");
                    }
                    sb.AppendLine();
                    sb2.AppendLine();
                }

                string[] lemmatized = sb.ToString().Split(new string[] { "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);
                string[] ss = sb2.ToString().Split(new string[] { "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);

                sents = ss;

                Sentence[] output = new Sentence[lemmatized.Length];
                // if (lemmatized.Length == sents.Length)
                for (int i = 0; i < lemmatized.Length; i++)
                    output[i] = new Sentence(lemmatized[i], ss[i]);
                // else
                //     throw new Exception("Chyba: ruzny pocet vet po lemmatizaci. Original text: "+sents.Length + "\tLemmatized text:"+sb.ToString());
                return output;
            }
            #endregion

            #region dummy-normalizer
            if (engine == Lemmatizer.dummy)
            {
                StringBuilder sb = new StringBuilder();
                string[] lemmasent = new string[sents.Length];
                int c = 0;
                foreach (string s in sents)
                {
                    File.WriteAllText("text.txt", s, Encoding.UTF8);

                    ProcessStartInfo si = new ProcessStartInfo(pythonFile.FullName);
                    si.Arguments = "unitok.py -n -s text.txt";
                    si.UseShellExecute = false;
                    si.RedirectStandardOutput = true;

                    p = new Process();
                    p.StartInfo = si;
                    p.Start();

                    StreamReader sr = p.StandardOutput;

                    string vert = sr.ReadToEnd();
                    sr.Close();
                    lemmasent[c] = vert.Trim();
                    c++;
                }

                Sentence[] output = new Sentence[lemmasent.Length];
                for (int i = 0; i < output.Length; i++)
                    output[i] = new Sentence(lemmasent[i], sents[i]);

                return output;
            }
            #endregion

            throw new Exception("Impossible exception during lemmatization.");
        }

        private string[] LemmatizeSentenciesWithFMorph(string[] sents, int id)
        {
            StringBuilder sb = new StringBuilder();
            if (!File.Exists("out" + id + ".txt"))
            {
                foreach (string s in sents)
                {
                    sb.AppendLine(s + "#$&");
                }
                string line = sb.ToString();

                // id = DateTime.Now.Ticks;
                line = line.Replace("\n", "\r\n");
                line = line.Replace("\r\r", "\r");
                TextWriter lineWriter = new StreamWriter("tmp" + id + ".txt", false, win);
                lineWriter.Write(line);
                lineWriter.Close();
                string argumenty = "FMorph/FMAnawin.pl NOSTDIO tmp" + id + ".txt out" + id + ".txt FMorph/CZE-a.il2 demo";
                //Console.WriteLine(argumenty);
                System.Diagnostics.Process proc = new System.Diagnostics.Process();
                proc.StartInfo.FileName = "perl";
                proc.StartInfo.Arguments = argumenty;
                proc.StartInfo.UseShellExecute = false;

                proc.StartInfo.RedirectStandardOutput = false;
                proc.StartInfo.RedirectStandardError = false;
                proc.Start();
                proc.WaitForExit();
            }
            List<string> vety = new List<string>();
            List<string> slovaList = new List<string>();
            TextReader tr = new StreamReader("out" + id + ".txt", win);
            string lemma = tr.ReadToEnd();
            tr.Close();


            if (File.Exists("tmp" + id + ".txt"))
                File.Delete("tmp" + id + ".txt");
            else
                throw new FileNotFoundException("Nebyl vytvořen soubor s textem.");

            if (File.Exists("out" + id + ".txt"))
                File.Delete("out" + id + ".txt");
            else
                throw new FileNotFoundException("Nebyl vytvořen soubor s lématy.");

            lemma = lemma.Replace("\r", "");
            string[] slova = lemma.Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);
            sb = new StringBuilder();
            bool nahrazeno = true;
            string puvodni = null;
            string nahrada = null;
            string predchozi = null;
            int count = 0;
            for (int i = 0; i < slova.Length; i++)
            {
                string temp = slova[i].ToLower();
                if (temp.Contains("moci"))
                    Console.WriteLine();
                if (temp.Contains("\t\t"))
                    continue;

                if (temp.Contains("&amp;") || temp.Contains("#") || temp.Contains("$"))
                {
                    count++;
                    if (count == 6)
                    {
                        foreach (string slovo in slovaList)
                        {
                            sb.Append(slovo + " ");
                        }
                        count = 0;
                        vety.Add(sb.ToString());
                        sb = new StringBuilder();
                        slovaList.Clear();
                        nahrada = null;
                        nahrazeno = false;
                    }
                    continue;
                }
                count = 0;

                while (temp.Contains("_") || temp.Contains("-") || temp.Contains("`"))
                {
                    int index = temp.IndexOf("_");
                    if (index == -1)
                    {
                        index = temp.IndexOf("-");
                        if (index == -1)
                        {
                            index = temp.IndexOf("`");
                        }
                    }
                    if (index != -1)
                        temp = temp.Substring(0, index);
                }
                if (nahrazeno)
                {
                    puvodni = temp;
                    nahrazeno = false;
                    continue;
                }
                if (temp.Contains("\t"))
                {
                    if (temp.Replace("\t", "") == puvodni)
                        nahrada = puvodni;
                    else if (nahrada == null)
                        nahrada = temp.Replace("\t", "");
                }
                else
                {
                    if (nahrada == null)
                        nahrada = puvodni;
                    if (!slovaList.Contains(nahrada))
                        slovaList.Add(nahrada);
                    nahrada = null;
                    nahrazeno = true;
                    i--;
                }
                if (predchozi != nahrada)
                {
                    predchozi = nahrada;
                    if (nahrada != null)
                        slovaList.Add(nahrada);
                }
            }

            //vyprazdneni seznamu slov pri lemantizani nadpisu
            sb = new StringBuilder();
            foreach (string slovo in slovaList)
            {
                sb.Append(slovo + " ");
            }
            vety.Add(sb.ToString());

            int prazdne = 0;
            foreach (string veta in vety)
                if (veta == "")
                    prazdne++;

            string[] vetyS = new string[vety.Count - prazdne];
            int c = 0;
            foreach (string veta in vety)
                if (veta != "")
                    vetyS[c++] = veta;

            return vetyS;
        }

        private string LemmatizeWordWithHunspell(string word, Hunspell speller)
        {
            if (word.Length == 0) return "";
            else if (word.Length == 1) return word.Trim();
            else
            {
                if (speller.Spell(word))
                {
                    string theOne = "";
                    List<string> outcome = speller.Analyze(word.Trim());
                    if (outcome.Count == 0)
                    {
                        return word.Trim();
                    }
                    else
                    {
                        foreach (string o in outcome)
                        {
                            string tmp = Regex.Match(o, @"st:(.+?)\b").Groups[1].ToString();
                            if (theOne == "")
                                theOne = tmp;
                            if (tmp.Trim().ToLower() == word.Trim().ToLower())
                                theOne = tmp;
                        }
                        return theOne;
                    }
                }
                else
                    return word.Trim();

            }
        }

        /// <summary>
        /// Method splits single line to array of sentences
        /// </summary>
        /// <param name="line"></param>
        /// <returns></returns>
        public string[] Raw2sents(string line)
        {
            List<string> sents = new List<string>();

            line = Regex.Replace(line, "\r", " ");
            line = Regex.Replace(line, "\n", " ");
            line = Regex.Replace(line, "  +", " ");

            //pravidla kdy se nedetekuje nova veta!
            Dictionary<string, string> nahrady = new Dictionary<string, string>();
            nahrady.Add(@"([ =]\d+)! (\p{Ll})", "$1<faktorial> $2");                                     // 654! neco || =65! neco
            nahrady.Add(@"( \p{Lu})\. ?(\p{Lu})\. ?(\p{Lu})", "$1<zkratka> $2<zkratka> $3");            // F. X. Salda
            nahrady.Add(@"(\W\p{Lu})\. ?(\p{Lu})", "$1<zkratka> $2");                                   // K. Capek
            nahrady.Add(@"( \d+)\.( ?\d+)", "$1<cislovka>$2");                                          // 9. 9.
            nahrady.Add(@"( \d+)\.( ?\p{Lu})", "$1<cislovka>$2");                                       // 9. Srpna
            nahrady.Add(@"( \d+)\.( ?\p{Ll})", "$1<cislovka>$2");                                       // 9. srpna
            nahrady.Add(@"\.\.\. (\p{Ll})", "<trojtecka> $1");                                          //... a tak to je
            nahrady.Add(@"\.( ?\p{Ll})", "<zkratka>$1");                                                //kanec H. se chlubil

            //pravidla na zpetne prepisy
            Dictionary<string, string> zpetneNAhrady = new Dictionary<string, string>();
            zpetneNAhrady.Add("<zkratka>", ".");
            zpetneNAhrady.Add("<faktorial>", "!");
            zpetneNAhrady.Add("<cislovka>", ".");
            zpetneNAhrady.Add("<trojtecka>", "...");

            //pridej zkratky
            foreach (string zkratka in zkratky)
            {
                try
                {
                    nahrady.Add(@"( " + zkratka + @")\. ", "$1<zkratka> ");
                }
                catch (Exception) { ; }
            }

            //aplikace prepisovacich pravidel
            foreach (string key in nahrady.Keys)
                while (Regex.IsMatch(line, key))
                    line = Regex.Replace(line, key, nahrady[key]);

            //pravidla pro zalomeni => vlozeni znacek na zalomeni
            line = Regex.Replace(line, @"\. ?(\(.+?[\.\?!]\)) (\p{Lu})", @".\r\n$1\r\n$2");             //neco. (veta v zavorce.) A vy můžete
            line = Regex.Replace(line, "([\\.\\?!][\"']) (\\p{Lu})", @"$1\r\n$2");                      //"veta." Dalsi veta   
            line = Regex.Replace(line, @"(°[CFK])<zkratka> (\p{Lu})", @"$1.\r\n$2");                            //19 °C. Ale     
            line = Regex.Replace(line, @"\. ", @".\r\n");
            line = Regex.Replace(line, @"! ", @"!\r\n");
            line = Regex.Replace(line, @"\? ", @"?\r\n");

            //zapis oznackovanyho textu
            /*
            using (TextWriter writer = new StreamWriter("line se znackama.txt", false, Encoding.UTF8))
            {
                writer.Write(line);
            }
            */

            //zpetny prepis
            foreach (string key in zpetneNAhrady.Keys)
                line = line.Replace(key, zpetneNAhrady[key]);

            //zalomeni vet
            sents = line.Split(new string[] { @"\r\n" }, StringSplitOptions.RemoveEmptyEntries).ToList();

            return sents.ToArray();
        }

        /// <summary>
        /// Method returns the shortest possible sentences.
        /// </summary>
        /// <param name="clauses"></param>
        /// <returns></returns>
        public string[] Clauses2Sentences(string[] clauses)
        {
            for (int c = 0; c < clauses.Length; c++)
            {
                string newClause = RemoveTags(clauses[c].Trim()) + ".";
                if (newClause.StartsWith(","))
                    newClause = newClause.Substring(2);
                newClause = newClause[0].ToString().ToUpper().ToCharArray()[0] + newClause.Substring(1);
                clauses[c] = newClause;
            }
            return clauses;
        }

        private string[] RemoveTags(IEnumerable<string> lines)
        {
            Regex morpTag = new Regex(@"\<phr\>.*?\:", RegexOptions.Compiled);
            string[] output = new string[lines.Count()];
            int c = 0;
            foreach (string line in lines)
            {
                string ln = morpTag.Replace(line, "").Trim();
                output[c] = ln;
                c++;
            }
            return output;
        }
        private string RemoveTags(string line)
        {
            Regex morpTag = new Regex(@"\<phr\>.*?\:", RegexOptions.Compiled);
            return morpTag.Replace(line, "").Trim();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="clauses">Array of clauses</param>
        /// <param name="shortest">If true: method returns the shortest possible sentences. If false: method returns sentences with dependent clauses.</param>
        /// <returns></returns>
        public string[] Clauses2Sentences(string[] clauses, bool shortestPossible)
        {
            if (shortestPossible)
            {
                return Clauses2Sentences(clauses);
            }
            // spojit pres podradici spojky

            List<string> sentences = new List<string>();
            string sentence = "";
            foreach (string line in RemoveTags(clauses.Reverse()))
            {
                if (line.StartsWith(","))
                {
                    string clause = line.Substring(2).Trim();
                    if (clause.StartsWith("v"))
                        clause = clause.Substring(clause.IndexOf(" ")).Trim();
                    bool dependent = false;
                    foreach (string interjunction in podradiciSpojky)
                        if (clause.StartsWith(interjunction))
                            dependent = true;

                    // nedetekuje podradici spojky
                    sentence = line + sentence;
                    if (!dependent)
                    {
                        sentences.Add(sentence.Substring(2));
                        sentence = "";
                    }
                }
                else
                {
                    sentence = line + sentence;
                    sentences.Add(sentence);
                    sentence = "";
                }
            }

            string[] sentArray = new string[sentences.Count];
            int s = sentences.Count;
            foreach (string sent in sentences)
                sentArray[--s] = sent[0].ToString().ToUpper() + sent.Substring(1) + ".";
            return sentArray;
        }

        /// <summary>
        /// vypocita frekvenci termu ve vetach
        /// </summary>
        public Dictionary<string, double> UnionSentencesTF(Sentence[] input)
        {
            Dictionary<string, double> tf = new Dictionary<string, double>();
            foreach (Sentence s in input)
                foreach (string word in s.Words.Keys)
                    if (tf.ContainsKey(word))
                        tf[word] += s.Words[word];
                    else
                        tf.Add(word, s.Words[word]);
            return tf;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="tf"></param>
        /// <returns></returns>
        public Dictionary<string, double> RemoveStoplist(Dictionary<string, double> tf)
        {
            if (stoplist == null)
                stoplist = new HashSet<string>(new StreamReader(stopFile.FullName, Encoding.UTF8).ReadToEnd().Split(new string[] { "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries));

            List<string> remove = tf.Keys.Intersect(stoplist).ToList();

            foreach (string stop in remove)
                tf.Remove(stop);
            return tf;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public Dictionary<string, double> CalculateTF(Sentence[] input)
        {
            Dictionary<string, double> tf = new Dictionary<string, double>();
            foreach (Sentence s in input)
            {
                foreach (string word in s.Words.Keys)
                {
                    if (tf.ContainsKey(word))
                        tf[word]++;
                    else
                        tf.Add(word, 1);
                }
            }
            return tf;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="keyWords"></param>
        /// <param name="tf"></param>
        /// <returns></returns>
        public Dictionary<string, double> BoostKeyWords(IEnumerable<string> keyWords, Dictionary<string, double> tf)
        {
            foreach (string word in keyWords)
                if (tf.ContainsKey(word))
                    tf[word] *= 10;
            return tf;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public Sentence[] ReplaceSynonyms(Sentence[] input)
        {
            Sentence[] output = new Sentence[input.Length];

            //nacist slovnik (uz bych ho mel mit v pameti)
            if (theraurus == null)
                ReadThesaurus(synonymsFile.FullName);

            //projit slova textu, pokud jsou v thesauru, tak nahradit
            foreach (Sentence s in input)
            {
                List<string> ww = s.Words.Keys.ToList();
                foreach (string word in ww)
                {
                    foreach (string key in theraurus.Keys)
                    {
                        if (theraurus[key].Contains(word))
                        {
                            s.ReplaceWord(word, key);
                        }
                    }
                }
            }

            return input;
        }
    }
}
