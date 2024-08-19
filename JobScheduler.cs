using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace inspector
{
    /// <summary>
    /// JobScheduler is a Model class used to handle the long-running task/job scheduling system
    /// </summary>
    public class JobScheduler : INotifyPropertyChanged
    {
        /// <summary>
        /// Boilerplate code for the INotifyPropertyChanged interface
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }


        private Dictionary<int, string> _activeJobs = new();
        /// <summary>
        /// ActiveJobs stores the pending jobs as a dictionary of [Job ID, job description] pairs exposed as an IEnumerable of string
        /// </summary>
        public IEnumerable<string> ActiveJobs
        {
            get => _activeJobs.Values;
        }



        private int _jobID = 0;
        /// <summary>
        /// BeginJob() posts a new pending job with the specified message and returns a unique job identifier for the statusbar job indicator
        /// </summary>
        public int BeginJob(string message)
        {
            _activeJobs[_jobID] = message;
            OnPropertyChanged(nameof(ActiveJobs));
            return _jobID++;
        }


        /// <summary>
        /// EndJob() marks the specified unique job identifier as completed for the statusbar job indicator
        /// </summary>
        public void EndJob(int jobID)
        {
            _activeJobs.Remove(jobID);
            OnPropertyChanged(nameof(ActiveJobs));
        }
    }
}
