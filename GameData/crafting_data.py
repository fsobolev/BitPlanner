import copy
import json
import os.path

root = 'BitCraft_GameData/server/region'
crafting_recipes = json.load(open(f'{root}/crafting_recipe_desc.json'))
extraction_recipes = json.load(open(f'{root}/extraction_recipe_desc.json'))
items = json.load(open(f'{root}/item_desc.json'))
item_lists = json.load(open(f'{root}/item_list_desc.json'))
cargos = json.load(open(f'{root}/cargo_desc.json'))
enemies = json.load(open(f'{root}/enemy_desc.json'))

cargo_offset = 0xffffffff
recipes_order_overrides = {
	# Fertilizer: from berries, from flowers, from fish, from waste
	1100001: [1130004, 1130005, 1110020, 1582847061],
	2100001: [2130004, 2130005, 2110020, 1974958555],
	3100001: [3130004, 3130005, 3110020, 2127360112],
	4100001: [4130004, 4130005, 4110020, 1788612243],
	5100001: [5130004, 5130005, 5110020, 2083052337],
	6100001: [6130004, 6130005, 6110020, 1110458355],
	18012337: [1695618883, 418805707, 981574528, 2059094272],
	1895003720: [1971526319, 1935440864, 126302392, 1800829536],
	1329815128: [1608154701, 1359045035, 1831764501, 1357296296],
	52170614: [51716987, 1879591029, 1588421618],
	# Study Journal: from carvings, from diagrams
	1210004: [1210037, 1210038],
	2210004: [2210037, 2210038],
	3210004: [3210037, 3210038],
	4210004: [4210037, 4210038],
	5210004: [5210037, 5210038],
	6210004: [6210037, 6210038],
	# Chemical Catalyst: from embergrain, from wispveave, from starbulb
	1573699181: [1100005, 1100015, 1100008],
	283591176: [2100005, 2100015, 2100008],
	1020188259: [3100005, 3100015, 3100008],
	98643046: [4100005, 4100015, 4100008],
	1030159712: [5100005, 5100015, 5100008],
	389773215: [6100005, 6100015, 6100008],
	1569513170: [1008417993, 53729166, 2115422084],
	1359102754: [1024977657, 2129960803, 757946117],
	1527038072: [2120609742, 1530487215, 1537113956],
	391105297: [1239830729, 1181587439, 21938971],
}
crafting_data = {}


def find_recipes(id, is_cargo = False):
	recipes = []
	item_type = 1 if is_cargo else 0
	for recipe in crafting_recipes:
		for result in recipe['crafted_item_stacks']:
			if result[0] == id:
				if result[2][0] != item_type:
					continue

				consumed_items = []
				consumes_itself = False

				for item in recipe['consumed_item_stacks']:
					if item[0] == id:
						consumes_itself = True
						break
					consumed_id = item[0] + (cargo_offset if item[2][0] == 1 else 0)
					consumed_items.append({ 'id': consumed_id, 'quantity': item[1] })
				
				if consumes_itself:
					continue

				recipe_data = {
					'level_requirements': recipe['level_requirements'][0], 
					'consumed_items': consumed_items,
					'output_quantity': result[1],
					'possibilities': {}
				}
				recipes.append(recipe_data)
	return recipes

def find_extraction_skill(id, is_cargo = False):
	skill = -1
	found = False
	item_type = 1 if is_cargo else 0

	for recipe in extraction_recipes:
		for result in recipe['extracted_item_stacks']:
			if result[0][1][0] == id:
				if result[0][1][2][0] != item_type:
					continue
				skill = recipe['level_requirements'][0][0]
				found = True
				break
		if found:
			return skill
	
	for enemy in enemies:
		for result in enemy['extracted_item_stacks']:
			if result[0][1][0] == id:
				if result[0][1][2][0] != item_type:
					continue
				skill = enemy['experience_per_damage_dealt'][0][0]
				found = True
				break
		if found:
			return skill
		
	return skill

def get_recipe_priority(target_id, recipe):
	if target_id in recipes_order_overrides.keys():
		for item in recipe['consumed_items']:
			if item['id'] in recipes_order_overrides[target_id]:
				return recipes_order_overrides[target_id].index(item['id'])
	item = recipe['consumed_items'][0]
	return (item['quantity'] + (1000 if item['id'] > cargo_offset else 0)) * crafting_data[item['id']]['rarity'] * 100 / recipe['output_quantity'] + sum(map(int, str(item['id'])))


print('Collecting items...')
for item in items:
	id = item['id']
	if id > cargo_offset:
		print(f'FATAL: item id {id} exceeds uint32 range')
		os.exit(1)

	crafting_data[id] = {
		'name': item['name'],
		'tier': item['tier'],
		'rarity': item['rarity'][0],
		'icon': item['icon_asset_name'].replace('GeneratedIcons/', ''),
		'recipes': find_recipes(id),
		'extraction_skill': find_extraction_skill(id)
	}

print('Collecing cargos...')
for item in cargos:
	id = item['id']
	if id > cargo_offset:
		print(f'FATAL: cargo id {id} exceeds uint32 range')
		os.exit(1)

	crafting_data[cargo_offset + id] = {
		'name': item['name'],
		'tier': item['tier'],
		'rarity': item['rarity'][0],
		'icon': item['icon_asset_name'].replace('GeneratedIcons/', ''),
		'recipes': find_recipes(id, True),
		'extraction_skill': find_extraction_skill(id, True)
	}

print('Checking icons...')
missing_icons = []
for item in crafting_data.values():
	icon = item['icon']
	if not os.path.exists(f'../BitPlanner/Assets/{icon}.png'):
		if os.path.exists(f'../BitPlanner/Assets/{icon.replace('Other/', '')}.png'):
			item['icon'] = icon.replace('Other/', '')
		else:
			missing_icons.append(icon)
if len(missing_icons) > 0:
	print('Missing icons:')
	for icon in sorted(set(missing_icons)):
		print('  ' + icon)

print('Reorganizing recipes...')
for item in items:
	id = item['id']
	list_id = item['item_list_id']
	if list_id == 0 or item['tier'] < 0:
		continue
	del crafting_data[id]

	for item_list in item_lists:
		if item_list['id'] != list_id:
			continue

		possible_recipes = {}
		for possibility in item_list['possibilities']:
			chance = possibility[0]

			for details in possibility[1]:
				target_id = details[0] + (cargo_offset if details[2][0] == 1 else 0)
				if not target_id in crafting_data.keys():
					continue
				if crafting_data[target_id]['extraction_skill'] < 0:
					crafting_data[target_id]['extraction_skill'] = find_extraction_skill(id)

				if not target_id in possible_recipes.keys():
					possible_recipes[target_id] = {}
				quantity = details[1]
				if not quantity in possible_recipes[target_id]:
					possible_recipes[target_id][quantity] = 0.0
				possible_recipes[target_id][quantity] += chance

		recipes = find_recipes(id)
		for target_id, possibilities in possible_recipes.items():
			if not target_id in crafting_data.keys():
				print(f'Warning: no ID {target_id} in crafting data')
				continue
			new_recipes = copy.deepcopy(recipes)
			for recipe in new_recipes:
				recipe['possibilities'] = {k: possibilities[k] for k in sorted(possibilities)}
			crafting_data[target_id]['recipes'].extend(new_recipes)
		break

print('Cleanup and sort recipes...')
for key, value in crafting_data.items():
	recipes = value['recipes']
	deduplicated_recipes = {json.dumps(r, sort_keys=True) for r in recipes}
	recipes = [json.loads(r) for r in deduplicated_recipes]
	recipes.sort(key=lambda recipe: get_recipe_priority(key, recipe))
	value['recipes'] = recipes

json.dump(crafting_data, open('../BitPlanner/crafting_data.json', 'w'), indent=2)
