﻿using Lumia.Sense;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.UI.Core;
using Windows.UI.Xaml;

namespace Tracks.Utilities
{
    /// <summary>
    /// Data class for getting users activities 
    /// </summary>
    public class ActivityReader : INotifyPropertyChanged
    {
        #region Private members
        /// <summary>
        /// List of activities and durations
        /// </summary>
        private List<MyQuantifiedData> _listData = null;

        /// <summary>
        /// Data instance
        /// </summary>
        private static ActivityReader _activityReader;

        /// <summary>
        /// List of history data
        /// </summary
        private IList<ActivityMonitorReading> _historyData;

        /// <summary>
        /// Activity monitor instance
        /// </summary>
        private IActivityMonitor _activityMonitor = null;

        /// <summary>
        /// Activity instance
        /// </summary>
        private Activity _activityMode = Activity.Idle;

        /// <summary>
        /// Time window index, 0 = today, -1 = yesterday 
        /// </summary>
        private double _timeWindowIndex = 0;
        #endregion

        /// <summary>
        /// Occurs when a property value changes.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// This method is called by the Set accessor of each property. 
        /// The CallerMemberName attribute that is applied to the optional propertyName 
        /// parameter causes the property name of the caller to be substituted as an argument.
        /// </summary>
        /// <param name="propertyName"></param>
        private void NotifyPropertyChanged([CallerMemberName] String propertyName = "")
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        /// <summary>
        /// Constructor  
        /// </summary>
        public ActivityReader()
        {
            _listData = new List<MyQuantifiedData>();
        }

        /// <summary>
        /// Activity monitor property. Gets and sets the activity monitor
        /// </summary>
        public IActivityMonitor ActivityMonitorProperty
        {
            get
            {
                return _activityMonitor;
            }
            set
            {
                _activityMonitor = value;
            }
        }

        /// <summary>
        /// Create new instance of the class
        /// </summary>
        /// <returns>Data instance</returns>
        static public ActivityReader Instance()
        {
            if (_activityReader == null)
                _activityReader = new ActivityReader();
            return _activityReader;
        }

        /// <summary>
        /// Called when activity changes
        /// </summary>
        /// <param name="sender">Sender object</param>
        /// <param name="args">Event arguments</param>
        public async void activityMonitor_ReadingChanged(IActivityMonitor sender, ActivityMonitorReading args)
        {
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                this.ActivityEnum = args.Mode;
            });
        }

        /// <summary>
        /// Initializes activity monitor
        /// </summary>
        public async Task Initialize()
        {
            if (ActivityMonitorProperty == null)
            {
                if (await MapPage._instanceMap.CallSensorcoreApiAsync(async () => { ActivityMonitorProperty = await ActivityMonitor.GetDefaultAsync(); }))
                {
                    Debug.WriteLine("ActivityMonitorSimulator initialized.");
                }
                if (ActivityMonitorProperty != null)
                {
                    // Set activity observer
                    ActivityMonitorProperty.ReadingChanged += activityMonitor_ReadingChanged;
                    ActivityMonitorProperty.Enabled = true;
                    // read current activity
                    ActivityMonitorReading reading = null;
                    if (await MapPage._instanceMap.CallSensorcoreApiAsync(async () => { reading = await ActivityMonitorProperty.GetCurrentReadingAsync(); }))
                    {
                        if (reading != null)
                        {
                            this.ActivityEnum = reading.Mode;
                        }
                    }
                }
                else
                {
                    // nothing to do if we cannot use the API
                    // in a real app do make an effort to make the user experience better
                    return;
                }
                // Must call DeactivateAsync() when the application goes to background
                Window.Current.VisibilityChanged += async (sender, args) =>
                {
                    if (_activityMonitor != null)
                    {
                        await MapPage._instanceMap.CallSensorcoreApiAsync(async () =>
                        {
                            if (!args.Visible)
                            {
                                await _activityMonitor.DeactivateAsync();
                            }
                            else
                            {
                                await _activityMonitor.ActivateAsync();
                            }
                        });
                    }
                };
            }
        }

        /// <summary>
        /// Get the current activity
        /// </summary>
        public string CurrentActivity
        {
            get
            {
                return _activityMode.ToString().ToLower();
            }
        }

        /// <summary>
        /// Set the current activity
        /// </summary>
        public Activity ActivityEnum
        {
            set
            {
                _activityMode = value;
                NotifyPropertyChanged("CurrentActivity");
            }
        }

        /// <summary>
        /// Get the time window
        /// </summary>
        public double TimeWindow
        {
            get
            {
                return _timeWindowIndex;
            }
        }

        /// <summary>
        /// Set the time window to today
        /// </summary>
        public void NextDay()
        {
            if (_timeWindowIndex < 0)
            {
                _timeWindowIndex++;
                NotifyPropertyChanged("TimeWindow");
            }
        }

        /// <summary>
        /// Set the time window to previous day
        /// </summary>
        public void PreviousDay()
        {
            if (_timeWindowIndex >= -9)
            {
                _timeWindowIndex--;
                NotifyPropertyChanged("TimeWindow");
            }
        }

        /// <summary>
        /// List of activities occured during given time period.
        /// </summary>
        public IList<ActivityMonitorReading> History
        {
            get
            {
                return _historyData;
            }
            set
            {
                if (_historyData == null)
                {
                    _historyData = new List<ActivityMonitorReading>();
                }
                else
                {
                    _historyData.Clear();
                }
                _historyData = value;
                QuantifyData();
            }
        }

        /// <summary>
        /// Get the list of activities and durations 
        /// </summary>
        public List<MyQuantifiedData> ListData
        {
            get
            {
                return _listData;
            }
        }

        /// <summary>
        /// Populate the list of activities and durations to display in the UI 
        /// </summary>
        private void QuantifyData()
        {
            if (_listData != null)
            {
                _listData.Clear();
            }
            _listData = new List<MyQuantifiedData>();
            if (!Windows.ApplicationModel.DesignMode.DesignModeEnabled)
            {
                List<string> _activitiesList = new List<string>(Enum.GetNames(typeof(Activity)));
                Dictionary<Activity, int> indexer = new Dictionary<Activity, int>();
                TimeSpan[] _durations = new TimeSpan[_activitiesList.Count];
                Activity[] values = (Activity[])Enum.GetValues(typeof(Activity));
                for (int i = 0; i < values.Length; i++)
                {
                    indexer.Add(values[i], i);
                }
                // there could be days with no data (e.g. of phone was turned off
                if (_historyData.Count > 0)
                {
                    // first entry may be from previous time window, is there any data from current time window?
                    bool hasDataInTimeWindow = false;
                    // insert new fist entry, representing the last activity of the previous time window
                    // this helps capture that activity's duration but only from the start of current time window                    
                    ActivityMonitorReading first = _historyData[0];
                    if (first.Timestamp <= DateTime.Now.Date.AddDays(_timeWindowIndex))
                    {
                        // create new "first" entry, with the same mode but timestamp set as 0:00h in current time window
                        _historyData.Insert(1, new ActivityMonitorReading(first.Mode, DateTime.Now.Date.AddDays(_timeWindowIndex)));
                        // remove previous entry
                        _historyData.RemoveAt(0);
                        hasDataInTimeWindow = _historyData.Count > 1;
                    }
                    else
                    {
                        // the first entry belongs to the current time window
                        // there is no known activity before it
                        hasDataInTimeWindow = true;
                    }
                    // if at least one activity is recorded in this time window
                    if (hasDataInTimeWindow)
                    {
                        // insert a last activity, marking the begining of the next time window
                        // this helps capturing the correct duration of the last activity stated in this time window
                        ActivityMonitorReading last = _historyData.Last();
                        if (last.Timestamp < DateTime.Now.Date.AddDays(_timeWindowIndex + 1))
                        {
                            // is this today's time window
                            if (_timeWindowIndex == 0)
                            {
                                // last activity duration measured until this instant time
                                _historyData.Add(new ActivityMonitorReading(last.Mode, DateTime.Now));
                            }
                            else
                            {
                                // last activity measured until the begining of the next time index
                                _historyData.Add(new ActivityMonitorReading(last.Mode, DateTime.Now.Date.AddDays(_timeWindowIndex + 1)));
                            }
                        }
                        // calculate duration for each current activity by subtracting its timestamp from that of the next one
                        for (int i = 0; i < _historyData.Count - 1; i++)
                        {
                            ActivityMonitorReading current = _historyData[i];
                            ActivityMonitorReading next = _historyData[i + 1];
                            _durations[indexer[current.Mode]] += next.Timestamp - current.Timestamp;
                        }
                    }
                }
                // populate the list to be displayed in the UI
                for (int i = 0; i < _activitiesList.Count; i++)
                {
                    _listData.Add(new MyQuantifiedData(_activitiesList[i], _durations[i]));
                }
            }
            NotifyPropertyChanged("ListData");
        }
    }

    /// <summary>
    ///  Helper class to create a list of activities and their timestamp 
    /// </summary>
    public class MyQuantifiedData
    {
        /// <summary>
        ///  Constructor
        /// </summary>
        /// <param name="s">Activity name</param>
        /// <param name="i">Activity time</param>
        public MyQuantifiedData(string s, TimeSpan i)
        {
            //split activity string by capital letter
            ActivityName = System.Text.RegularExpressions.Regex.Replace(s, @"([A-Z])(?<=[a-z]\1|[A-Za-z]\1(?=[a-z]))", " $1");
            ActivityTime = i;
        }

        /// <summary>
        /// Activity name 
        /// </summary>
        public string ActivityName
        {
            get;
            set;
        }

        /// <summary>
        /// Activity time
        /// </summary>
        public TimeSpan ActivityTime
        {
            get;
            set;
        }
    }
}