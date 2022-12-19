using Io.Axoniq.Axonserver.Grpc.Control;

namespace AxonIQ.AxonServer.Connector;

internal class EventProcessorCollection
{
    private readonly Dictionary<EventProcessorName, Func<Task<EventProcessorInfo?>>> _suppliers;
    private readonly Dictionary<EventProcessorName, IEventProcessorInstructionHandler> _handlers;
    
    public EventProcessorCollection()
    {
        _suppliers = new Dictionary<EventProcessorName, Func<Task<EventProcessorInfo?>>>();
        _handlers = new Dictionary<EventProcessorName, IEventProcessorInstructionHandler>();
    }
    
    public bool AddEventProcessorInfoSupplier(EventProcessorName name, Func<Task<EventProcessorInfo?>> supplier)
    {
        if (_suppliers.Count == 0)
        {
            _suppliers.Add(name, supplier);
            return true;
        }
        _suppliers[name] = supplier;
        return false;
    }
    
    public bool RemoveEventProcessorInfoSupplier(EventProcessorName name, Func<Task<EventProcessorInfo?>> supplier)
    {
        if (_suppliers.TryGetValue(name, out var registered) && supplier.Equals(registered))
        {
            _suppliers.Remove(name);
            if (_suppliers.Count == 0)
            {
                return true;
            }
        }
        
        return false;
    }
    
    public void AddEventProcessorInstructionHandler(EventProcessorName name, IEventProcessorInstructionHandler handler)
    {
        _handlers[name] = handler;
    }
    
    public void RemoveEventProcessorInstructionHandler(EventProcessorName name, IEventProcessorInstructionHandler handler)
    {
        if (_handlers.TryGetValue(name, out var registered) && handler.Equals(registered))
        {
            _handlers.Remove(name);
        }
    }

    public IEnumerable<(EventProcessorName, Func<Task<EventProcessorInfo?>>)> GetAllEventProcessorInfoSuppliers()
    {
        return _suppliers.Select(pair => (pair.Key, pair.Value));
    }

    public Func<Task<EventProcessorInfo?>>? GetEventProcessorInfoSupplier(EventProcessorName name)
    {
        return _suppliers.TryGetValue(name, out var supplier) ? supplier : null;
    }

    public IEventProcessorInstructionHandler? TryResolveEventProcessorInstructionHandler(EventProcessorName name)
    {
        return _handlers.TryGetValue(name, out var handler) ? handler : null;
    }
}