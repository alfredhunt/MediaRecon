namespace ApexBytez.MediaRecon.Events
{
    public abstract class GenericEvent<T>
    {
        public T Data { get; private set; }

        public GenericEvent(T data) { Data = data; }
    }
}
