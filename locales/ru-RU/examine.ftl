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
    [female] { $band ->
        [young] Это молодая женщина.
        [middle] Это женщина средних лет.
        [old] Это пожилая женщина.
       *[unknown] Это женщина.
        }
    [male] { $band ->
        [young] Это молодой мужчина.
        [middle] Это мужчина средних лет.
        [old] Это пожилой мужчина.
       *[unknown] Это мужчина.
        }
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
examine-body-missing = { $part ->
    [head] Нет головы!
    [left-arm] Нет левой руки!
    [right-arm] Нет правой руки!
    [left-leg] Нет левой ноги!
   *[right-leg] Нет правой ноги!
    }
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
examine-health-mangled = { $part ->
    [head] Голова сильно изувечена.
    [left-arm] Левая рука сильно изувечена.
    [right-arm] Правая рука сильно изувечена.
    [left-leg] Левая нога сильно изувечена.
   *[right-leg] Правая нога сильно изувечена.
    }
