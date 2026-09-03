namespace PortTraining.Core.Identity
{
    public sealed class StudentLoginResult
    {
        public bool Success { get; }
        public string Error { get; }
        public StudentSession Session { get; }

        private StudentLoginResult(bool success, string error, StudentSession session)
        {
            Success = success;
            Error = error;
            Session = session;
        }

        public static StudentLoginResult Accepted(StudentSession session)
        {
            return new StudentLoginResult(true, string.Empty, session);
        }

        public static StudentLoginResult Rejected(string error)
        {
            return new StudentLoginResult(false, error, null);
        }
    }
}

