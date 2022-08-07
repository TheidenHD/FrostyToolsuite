﻿using System;
using System.Collections.Generic;
using System.Linq;
using LevelEditorPlugin.Converters;
using LevelEditorPlugin.Editors;
using LevelEditorPlugin.Managers;

namespace LevelEditorPlugin.Entities
{
    public class InterfaceOutputPropertyChangedEventArgs : EventArgs
    {
        public int PropertyHash { get; private set; }
        public object NewValue { get; private set; }

        public InterfaceOutputPropertyChangedEventArgs(int propertyHash, object newValue)
        {
            PropertyHash = propertyHash;
            NewValue = newValue;
        }
    }

    public class InterfaceOutputEventTriggeredEventArgs : EventArgs
    {
        public int EventHash { get; private set; }

        public InterfaceOutputEventTriggeredEventArgs(int eventHash)
        {
            EventHash = eventHash;
        }
    }

    public class InterfaceDescriptor : IEbxType, IEntityData<FrostySdk.Ebx.InterfaceDescriptorData>, ISchematicsType
    {
        public FrostySdk.Ebx.InterfaceDescriptorData Data => data;
        public Guid FileGuid => fileGuid;
        public Guid InstanceGuid => instanceGuid;
        public EntityWorld World => world;

        protected FrostySdk.Ebx.InterfaceDescriptorData data;
        protected Entity parent;

        protected Guid fileGuid;
        protected Guid instanceGuid;

        protected List<IProperty> properties = new List<IProperty>();
        protected List<IEvent> events = new List<IEvent>();
        protected List<ILink> links = new List<ILink>();

        private Queue<Action> queuedEvents = new Queue<Action>();
        private List<Assets.Asset> loadedAssets = new List<Assets.Asset>();
        private EntityWorld world;

        public event EventHandler<InterfaceOutputPropertyChangedEventArgs> OnInterfaceOutputPropertyChanged;
        public event EventHandler<InterfaceOutputEventTriggeredEventArgs> OnInterfaceOutputEventTriggered;

        public InterfaceDescriptor(FrostySdk.Ebx.InterfaceDescriptorData inData, Entity inParent)
        {
            data = inData;
            parent = inParent;
            world = inParent.World;

            fileGuid = parent.FileGuid;
            instanceGuid = Guid.Parse(data.__InstanceGuid.ToString());

            foreach (var field in data.Fields)
            {
                var property = new Property<object>(this, field.Id, null, field.Name);
            }
            foreach (var evt in data.InputEvents)
            {
                var theEvent = new Event<OutputEvent>(this, evt.Id);
            }
            foreach (var evt in data.OutputEvents)
            {
                var theEvent = new Event<InputEvent>(this, evt.Id);
            }
        }

        public void BeginSimulation()
        {
            foreach (var property in properties)
            {
                var field = data.Fields.Find(f => f.Id == property.NameHash);
                if (field.ValueRef.Type != FrostySdk.IO.PointerRefType.Null)
                {
                    if (field.ValueRef.Type != FrostySdk.IO.PointerRefType.Internal)
                    {
                        var assetValue = LoadedAssetManager.Instance.LoadAsset<Assets.Asset>(this, field.ValueRef);
                        property.Value = assetValue;
                        loadedAssets.Add(assetValue);
                    }
                    else
                    {
                        property.Value = field.ValueRef.Internal;
                    }
                }
                else if (field.Value != "")
                {
                    property.Value = new DataFieldToValueConverter().Convert(field, typeof(object), null, null);
                }
            }
        }

        public void EndSimulation()
        {
            foreach (var asset in loadedAssets)
            {
                LoadedAssetManager.Instance.UnloadAsset(asset);
            }
        }

        public void Update_PreFrame()
        {
            lock (queuedEvents)
            {
                if (queuedEvents.Count > 0)
                {
                    var action = queuedEvents.Dequeue();
                    action.Invoke();
                }
            }
        }

        public void Update_PostFrame()
        {
        }

        public void AddPropertyConnection(int srcPort, ISchematicsType dstObject, int dstPort)
        {
            var property = GetProperty(srcPort);
            if (property != null)
            {
                property.AddConnection(dstObject, dstPort);
            }
        }

        public void AddEventConnection(int srcPort, ISchematicsType dstObject, int dstPort)
        {
            var evt = GetEvent(srcPort);
            if (evt != null)
            {
                evt.AddConnection(dstObject, dstPort);
            }
        }

        public void AddLinkConnection(int srcPort, ISchematicsType dstObject, int dstPort)
        {
            var link = GetLink(srcPort);
            if (link != null)
            {
                link.AddConnection(dstObject, dstPort);
            }
        }

        public IProperty GetProperty(int nameHash)
        {
            return properties.Find(p => p.NameHash == nameHash);
        }

        public IEvent GetEvent(int nameHash)
        {
            return events.Find(p => p.NameHash == nameHash);
        }

        public ILink GetLink(int nameHash)
        {
            return links.Find(l => l.NameHash == nameHash);
        }

        public void OnEvent(int eventHash)
        {
            var evt = GetEvent(eventHash);
            if (evt == null)
                return;

            if (evt is Event<InputEvent>)
            {
                OnInterfaceOutputEventTriggered?.Invoke(this, new InterfaceOutputEventTriggeredEventArgs(evt.NameHash));
                evt.Execute();
            }
            else
            {
                evt.Execute();
            }
        }

        public virtual void OnPropertyChanged(int propertyHash)
        {
            var property = GetProperty(propertyHash);
            if (property != null)
            {
                OnInterfaceOutputPropertyChanged?.Invoke(this, new InterfaceOutputPropertyChangedEventArgs(propertyHash, property.Value));
            }
        }

        public void OnLinkChanged(int linkHash)
        {
        }

        public void QueueEvent(int eventHash)
        {
            lock (queuedEvents)
            {
                queuedEvents.Enqueue(() => { OnEvent(eventHash); });
            }
        }

        public void QueueProperty(int propertyHash, string newValue)
        {
            lock (queuedEvents)
            {
                queuedEvents.Enqueue(() =>
                {
                    var property = GetProperty(propertyHash);
                    if (property != null)
                    {
                        try
                        {
                            property.Value = new DataFieldToValueConverter().Convert(newValue, typeof(object), null, null);
                        }
                        catch (InvalidCastException)
                        {
                        }
                    }
                });
            }
        }

        public void Destroy()
        {
        }
    }
}
