examine-tab-overview = Обзор
examine-tab-health = Здоровье
examine-tab-armor = Броня
examine-tab-weapon = Оружие
examine-door-open = Открыта.
examine-door-opening = Открывается.
examine-door-closing = Закрывается.
examine-door-shut = Закрыта.
examine-door-bolted = Болты опущены.
examine-door-emergency = Горит лампа аварийного доступа.
examine-door-unsafe = Датчик безопасности не работает.
examine-armor-covers = Закрывает { $zone ->
    [torso] торс
    [head] голову
    [arm] руки
   *[leg] ноги
    }.
examine-armor-blocks = Блокирует { $percent }% { $type ->
    [blunt] дробящего
    [slash] режущего
    [piercing] колющего
   *[heat] теплового
    } урона.
examine-wall-mounted = К ней что-то прикручено.
examine-body-identity = { $sex ->
    [female] Это { $band ->
        [young] молодая
        [middle] женщина средних лет,
        [old] пожилая
       *[unknown] { "" }
        } { $species ->
       *[human] человек
        }.
    [male] Это { $band ->
        [young] молодой
        [middle] мужчина средних лет,
        [old] пожилой
       *[unknown] { "" }
        } { $species ->
       *[human] человек
        }.
   *[none] Это { $species ->
       *[human] человек
        }.
    }
examine-body-dead = { $sex ->
    [female] Она мертва.
    [male] Он мёртв.
   *[none] Оно мертво.
    }
examine-body-ssd = { $sex ->
    [female] Она смотрит пустым, отсутствующим взглядом и ни на что не реагирует. Возможно, скоро придёт в себя.
    [male] Он смотрит пустым, отсутствующим взглядом и ни на что не реагирует. Возможно, скоро придёт в себя.
   *[none] Оно смотрит пустым, отсутствующим взглядом и ни на что не реагирует. Возможно, скоро придёт в себя.
    }
examine-body-holding = { $sex ->
    [female] Она держит { $item } в
    [male] Он держит { $item } в
   *[none] Оно держит { $item } в
    } { $hand ->
    [left] левой
   *[right] правой
    } руке.
examine-body-missing = { $side ->
    [left] Нет левой
    [right] Нет правой
   *[none] Нет
    } { $zone ->
    [head] головы
    [arm] руки
   *[leg] ноги
    }!
examine-health-too-far = Нужно подойти ближе.
examine-health-clean = Видимых ран нет.
examine-health-wound = { $band ->
    [minor] Лёгкие
    [moderate] Умеренные
   *[severe] Тяжёлые
    } { $type ->
    [blunt] ушибы
    [slash] порезы
    [piercing] проколы
   *[heat] ожоги
    }.
examine-health-mangled = { $side ->
    [left] Левая
    [right] Правая
   *[none] { "" }
    } { $zone ->
    [head] голова
    [arm] рука
   *[leg] нога
    } сильно изувечена.
