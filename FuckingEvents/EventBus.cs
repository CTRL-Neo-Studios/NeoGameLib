using System;
using System.Collections.Generic;
using System.Linq;

namespace NeoGameLib.FuckingEvents;

// HEY LOOK AT ME
// note to future self:
// ideally you wont be using this if you make a better system other than taking inspiration from minecraft java's slow and fucked up
// event bus architecture, which seems really efficient at first but is probably tech debt much muhc later
// i only wrote this in case if there's some event-related stuff, it's not meant for you to use it as the backbone of all update event threads
// YOU HAVE BEEN WARNED

public class EventBus
{
    private readonly Dictionary<Type, List<Delegate>> eventHandlers = new();

    public void Subscribe<T>(Action<T> handler)
    {
        if (!eventHandlers.ContainsKey(typeof(T)))
            eventHandlers[typeof(T)] = new(); // new event tracking list for new types in the dict
        
        eventHandlers[typeof(T)].Add(handler);
    }

    public void Publish<T>(T eventData)
    {
        if (eventHandlers.TryGetValue(typeof(T), out List<Delegate> handlers))
        {
            foreach (Delegate handler in handlers.ToList())
            {
                if (handler is Action<T>)
                    (handler as Action<T>).Invoke(eventData); // csharp intellisense pls stfu you are worse than the rust borrow checker
            }
        }
    }
}