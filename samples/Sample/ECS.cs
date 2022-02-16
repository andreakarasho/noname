using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Ecs
{
    public abstract class GameSystem
    {
        private readonly List<int> _entities = new List<int>();

        public void Process(float deltaTime)
        {
            foreach (var entity in _entities)
            {
                ProcessEntity(deltaTime, entity);
            }
        }

        public abstract void ProcessEntity(float deltaTime, int entity);

        protected int TypeMask;

        public int GetTypeMask()
        {
            return TypeMask;
        }

        public abstract void SetTypeMask(ComponentManager cm);

        internal void TrackNewEntity(int entity)
        {
            _entities.Add(entity);
        }
    }

    public abstract class GameSystem<T1> : GameSystem where T1 : unmanaged
    {
        public override void SetTypeMask(ComponentManager cm)
        {
            TypeMask |= cm.GetMask<T1>();
        }

        public override void ProcessEntity(float deltaTime, int entity) 
        {
            ref var t1Ref = ref Storages<T1>.GetStorage().GetComponent(entity);

            ProcessEntity(ref t1Ref);
        }

        public abstract void ProcessEntity(ref T1 t1Rfef);
    }

    public abstract class GameSystem<T1, T2> : GameSystem where T1 : unmanaged where T2 : unmanaged
    {
        public override void SetTypeMask(ComponentManager cm)
        {
            TypeMask |= cm.GetMask<T1>();
            TypeMask |= cm.GetMask<T2>();
        }

        public override void ProcessEntity(float deltaTime, int entity)
        {
            ref var t1Ref = ref Storages<T1>.GetStorage().GetComponent(entity);
            ref var t2Ref = ref Storages<T2>.GetStorage().GetComponent(entity);

            ProcessEntity(ref t1Ref, ref t2Ref);
        }

        public abstract void ProcessEntity(ref T1 t1Rfef, ref T2 t2Ref);
    }


    public class EntityManager
    {
        private readonly ComponentManager _componentManager;
        private readonly Dictionary<int, int> _entityComponentMasks = new Dictionary<int, int>();
        private readonly SystemProcessor _systemProcessor;

        private int _nextID = 1;

        public EntityManager(SystemProcessor sp, ComponentManager componentManager)
        {
            _systemProcessor = sp;
            _componentManager = componentManager;
        }

        public int CreateEntity()
        {
            var ret = _nextID;
            _nextID += 1;
            return ret;
        }

        public void AddComponent<T>(int entity) where T : unmanaged
        {
            AddComponent(entity, default(T));
        }

        public void AddComponent<T>(int entity, T component) where T : unmanaged
        {
            var storage = Storages<T>.GetStorage();
            var index = storage.Store(entity, component);
            OnComponentAdded(entity, _componentManager.GetMask<T>());
        }

        internal void OnComponentAdded(int entity, int componentMask)
        {
            _entityComponentMasks.TryGetValue(entity, out var mask);
            mask |= componentMask;
            _entityComponentMasks[entity] = mask;
            _systemProcessor.EntityChangedMask(entity, mask);
        }
    }

    public class ComponentManager
    {
        public readonly Dictionary<Type, int> _typeIDs = new Dictionary<Type, int>();

        private int _nextId;

        public int GetMask<T>()
        {
            var type = typeof(T);
            if (!_typeIDs.TryGetValue(type, out var typeId))
            {
                typeId = _nextId;
                _nextId += 1;
                _typeIDs[type] = typeId;
            }

            return 1 << typeId;
        }
    }

    public static class Storages<T> where T : unmanaged
    {
        //private static readonly Dictionary<Type, IComponentStorage<T>> _storages =
        //    new Dictionary<Type, IComponentStorage<T>>();

        //public static ComponentStorage<T> GetStorage<T>()
        //{
        //    var t = typeof(T);
        //    if (!_storages.TryGetValue(t, out var storage))
        //    {
        //        storage = new ComponentStorage<T>();
        //        _storages.Add(t, storage);
        //    }

        //    return (ComponentStorage<T>)storage;
        //}

        private readonly static ComponentStorage<T> _componentStorage = new ComponentStorage<T>();

        public static ComponentStorage<T> GetStorage()
        {
            return _componentStorage;
        }
    }

    public interface IComponentStorage<T> where T : unmanaged
    {
        int Store(int entity, T component); 
        ref T GetComponent(int entity);
        int GetStorageIndex(int entity);
    }

    public class ComponentStorage<T> : IComponentStorage<T> where T : unmanaged
    {
        private int[] _componentIndicesByEntity = new int[100];
        private int _nextID = 1;
        public T[] Components = new T[100];

        public unsafe int Store(int entity, T component)
        {
            var ret = _nextID;
            var ptr = Unsafe.AsPointer(ref component);

            if (Components.Length <= ret)
            {
                Array.Resize(ref Components, Components.Length * 2);
            }

            if (_componentIndicesByEntity.Length <= entity)
            {
                Array.Resize(ref _componentIndicesByEntity, _componentIndicesByEntity.Length * 2);
            }

            Components[ret] = Unsafe.AsRef<T>(ptr);
            _componentIndicesByEntity[entity] = ret;
            _nextID += 1;
            return ret;
        }

        public int GetStorageIndex(int entity)
        {
            return _componentIndicesByEntity[entity];
        }

        public unsafe ref T GetComponent(int entity) 
        {
           return ref Components[GetStorageIndex(entity)];
            //return ref Unsafe.AsRef<T1>(Unsafe.AsPointer(ref refVal));
        }
    }

    public class SystemProcessor
    {
        private readonly ComponentManager _componentManager;
        private readonly List<GameSystem> _systems = new List<GameSystem>();

        public SystemProcessor(ComponentManager componentManager)
        {
            _componentManager = componentManager;
        }

        public void RegisterSystem(GameSystem s)
        {
            s.SetTypeMask(_componentManager);
            _systems.Add(s);
        }

        public void Process(float deltaTime)
        {
            foreach (var system in _systems)
            {
                system.Process(deltaTime);
            }
        }

        internal void EntityChangedMask(int entity, int mask)
        {
            foreach (var system in _systems)
            {
                var systemMask = system.GetTypeMask();
                if ((systemMask & mask) == systemMask)
                {
                    system.TrackNewEntity(entity);
                }
            }
        }
    }
}