namespace Phabrico.Interpreters
{
    /// <summary>
    /// Represents a fake Phabricator interpreter to make it easy on unit-testing
    /// </summary>
    public class UnitTestInterpreter : Interpreter
    {
        /// <summary>
        /// Name of the interpreter
        /// </summary>
        public override string Name
        {
            get
            {
                return "phutil_test_block_interpreter";
            }
        }

        /// <summary>
        /// Returns some text based on some given parameters
        /// </summary>
        /// <param name="parameterList"></param>
        /// <param name="content"></param>
        /// <returns></returns>
        public override string Parse(string parameterList, string content)
        {
            return string.Format("Content: ({0})\r\nArgv: ({1})\r\n", content, parameterList);
        }
    }
}
