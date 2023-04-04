using Io.Axoniq.Axonserver.Grpc.Control;

namespace AxonIQ.AxonServer.Connector;

internal class EventProcessorCollection
{
    private readonly Dictionary<EventProcessorName, Func<Task<EventProcessorInfo?>>> _suppliers = new();
    private readonly Dictionary<EventProcessorName, IEventProcessorInstructionHandler> _handlers = new();
    
    public void RegisterEventProcessor(
        EventProcessorName name, 
        Func<Task<EventProcessorInfo?>> supplier,
        IEventProcessorInstructionHandler handler)
    {
        _suppliers[name] = supplier ?? throw new ArgumentNullException(nameof(supplier));
        _handlers[name] = handler ?? throw new ArgumentNullException(nameof(handler));
    }
    
    public bool TryGetEventProcessorInfoSupplier(EventProcessorName name, out Func<Task<EventProcessorInfo?>>? supplier)
    {
        return _suppliers.TryGetValue(name, out supplier);
    }

    public IReadOnlyCollection<(EventProcessorName, Func<Task<EventProcessorInfo?>>)>
        GetAllEventProcessorInfoSuppliers()
    {
        return _suppliers
            .Select(pair => (pair.Key, pair.Value))
            .ToArray();
    }

    public bool TryGetEventProcessorInstructionHandler(EventProcessorName name, out IEventProcessorInstructionHandler? handler)
    {
        return _handlers.TryGetValue(name, out handler);
    }

    public void UnregisterEventProcessor(
        EventProcessorName name,
        Func<Task<EventProcessorInfo?>> supplier,
        IEventProcessorInstructionHandler handler)
    {
        if (_suppliers.TryGetValue(name, out var registeredSupplier) && supplier.Equals(registeredSupplier))
        {
            _suppliers.Remove(name);
        }
        if (_handlers.TryGetValue(name, out var registeredHandler) && handler.Equals(registeredHandler))
        {
            _handlers.Remove(name);
        }
    }

    public int Count => _suppliers.Count;
}