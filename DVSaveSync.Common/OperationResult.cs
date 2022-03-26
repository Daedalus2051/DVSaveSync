namespace DVSaveSync.Common
{
    public class OperationResult
    {
        /// <summary>
        /// Denotes whether the operation was successful or not. Default value is True.
        /// </summary>
        public bool IsSuccess { get; set; }
        /// <summary>
        /// A collection of messages related to the operations that occurred.
        /// </summary>
        public List<string> Messages { get; set; }

        public OperationResult()
        {
            Messages = new List<string>();
            IsSuccess = true;
        }
        /// <summary>
        /// Adds a message to the collection.
        /// </summary>
        /// <param name="message"></param>
        public void AddMessage(string message)
        {
            if (string.IsNullOrEmpty(message)) return;
            Messages.Add(message);
        }
        /// <summary>
        /// Adds a message to the collection and sets the IsSuccess bool to false.
        /// </summary>
        /// <param name="message"></param>
        public void AddFailureMessage(string message)
        {
            AddMessage(message);
            IsSuccess = false;
        }

        public override string ToString()
        {
            string output = "";
            foreach (var item in Messages)
            {
                output += $"{item}{Environment.NewLine}";
            }
            return output;
        }
    }
}
