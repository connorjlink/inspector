using inspector;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;
using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Timers;
using System.Windows.Documents;

// AI-assisted
public class MqttScheduler
{
    private class ScheduledMessage
    {
        public System.Timers.Timer _timer;
        public bool _paused;

        public ScheduledMessage(System.Timers.Timer timer)
        {
            _timer = timer;
            _paused = false;
        }
    }

    private ViewModel _viewmodel;
    private ConcurrentDictionary<string, ScheduledMessage> _scheduledMessages = new();

    //private ConcurrentDictionary<string, System.Timers.Timer> ScheduledMessages
    //{
    //    get
    //    {
    //        return _scheduledMessages;
    //    }
    //}

    public MqttScheduler(ViewModel viewmodel)
    {
        _viewmodel = viewmodel;
    }

    public int TotalMessageCount()
    {
        // NOTE: here we just want the total number of periodic messages (paused or not) so using the `Count` property is OK
        return _scheduledMessages.Count;
    }

    public int UnpausedMessageCount()
    {
        // NOTE: we can't use the `Count` property here because some messages might be paused

        var count = 0;

        foreach (var message in _scheduledMessages.Values)
        {
            if (!message._paused)
            {
                count++;
            }
        }

        return count;
    }

    public bool IsMessageScheduled(string topic)
    {
        return _scheduledMessages.ContainsKey(topic);
    }

    public bool IsMessagePaused(string topic)
    {
        if (_scheduledMessages.TryGetValue(topic, out var message))
        {
            if (message._paused)
            {
                return true;
            }
        }

        return false;
    }

    public void PauseAll()
    {
        foreach (var message in _scheduledMessages.Values)
        {
            message._timer.Stop();
            // NOTE: not updating the pause value so that we only resume the correct ones later on
            //message._paused = true;
        }
    }

    public void ResumeAll()
    {
        foreach (var message in _scheduledMessages.Values)
        {
            if (!message._paused)
            {
                message._timer.Start();
            }
        }
    }

    public void KillAll()
    {
        // TODO: is stopping all of the timers first even necessary?
        foreach (var message in _scheduledMessages.Values)
        {
            message._timer.Stop();
        }

        _scheduledMessages.Clear();
    }

    public async Task PublishAsync(MqttApplicationMessage applicationMessage)
    {
        await ViewModel._mqttClient.PublishAsync(applicationMessage);
        _viewmodel._currentSentCount++;
    }

    public void ScheduleMessage(MqttApplicationMessage applicationMessage, int periodicRate)
    {
        var topic = applicationMessage.Topic;

        if (_scheduledMessages.ContainsKey(topic))
        {
            throw new InvalidOperationException($"A schedule for '{topic}' already exists");
        }

        var timer = new System.Timers.Timer(periodicRate)
        {
            AutoReset = true,
            Enabled = true
        };

        timer.Elapsed += async (sender, e) => await PublishAsync(applicationMessage);

        // NOTE: the ScheduledMessage constructor defaults to *not paused*
        _scheduledMessages[topic] = new ScheduledMessage(timer);

        _viewmodel.WriteConsole($"Started transmitting {topic} every {periodicRate} ms", LogLevel.Info);
    }

    public bool TryRemoveMessageSchedule(MqttApplicationMessage applicationMessage)
    {
        var topic = applicationMessage.Topic;

        if (_scheduledMessages.TryRemove(topic, out var message))
        {
            _viewmodel.WriteConsole($"Stopped transmitting {topic}", LogLevel.Info);
            message._timer.Stop();
            message._timer.Dispose();
            return true;
        }

        return false;
    }

    public bool TryPauseMessage(string topic)
    {
        if (_scheduledMessages.TryGetValue(topic, out var message))
        {
            _viewmodel.WriteConsole($"Paused {topic}", LogLevel.Info);
            message._timer.Stop();
            message._paused = true;
            return true;
        }

        return false;
    }

    public bool TryResumeMessage(string topic)
    {
        if (_scheduledMessages.TryGetValue(topic, out var message))
        {
            _viewmodel.WriteConsole($"Resumed {topic}", LogLevel.Info);
            message._timer.Start();
            message._paused = false;
            return true;
        }

        return false;
    }
}