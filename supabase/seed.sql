-- Fridge Chef starter data. ALREADY APPLIED to the live project; Phase 2 will extend it.
-- Khmer titles/descriptions need review by a Khmer speaker. ingredients_km / steps_km are empty on purpose:
-- the app should fall back to English when they are empty.

-- Phase 2: flags, adjectives and Myanmar move here too (they are set by migration 20261113000000 on the live project).
insert into public.cuisines (code,name_en,name_km,sort_order) values
('KH','Cambodia','កម្ពុជា',1),('VN','Vietnam','វៀតណាម',11),('LA','Laos','ឡាវ',12),
('CN','China','ចិន',13),('JP','Japan','ជប៉ុន',14),('KR','Korea','កូរ៉េ',15),('IN','India','ឥណ្ឌា',16),
('ID','Indonesia','ឥណ្ឌូនេស៊ី',17),('MY','Malaysia','ម៉ាឡេស៊ី',18),('PH','Philippines','ហ្វីលីពីន',19),('SG','Singapore','សិង្ហបុរី',20),
('US','United States','សហរដ្ឋអាមេរិក',21),('FR','France','បារាំង',22),('IT','Italy','អ៊ីតាលី',23),('MX','Mexico','ម៉ិកស៊ិក',24),
('TR','Turkey','តួកគី',25),('GR','Greece','ក្រិក',26),('ES','Spain','អេស្ប៉ាញ',27),('GB','United Kingdom','ចក្រភពអង់គ្លេស',28);

insert into public.recipes (title_en,title_km,description_en,description_km,calories,cuisine,category,prep_minutes,servings,difficulty,is_trending,ingredients_en,steps_en) values
('Fish Amok','អាម៉ុកត្រី','Soft coconut fish curry steamed in banana leaf.','ការីត្រីដូងទន់ៗ ចំហុយក្នុងស្លឹកចេក។',420,'KH','main',45,4,'medium',true,
 '["500 g white fish fillet","200 ml coconut milk","2 tbsp kroeung paste","1 egg","1 tbsp fish sauce","Banana leaves"]',
 '["Cut the fish into pieces.","Mix kroeung paste, coconut milk, egg and fish sauce.","Fold in the fish.","Steam in banana leaf cups for 20 minutes."]'),
('Lok Lak','ឡុកឡាក់','Stir-fried beef with a lime and pepper dip.','សាច់គោឆាជាមួយទឹកត្រឡាច់ម្ទេសខ្មុំ។',510,'KH','main',30,2,'easy',true,
 '["300 g beef sirloin","2 tbsp soy sauce","1 tbsp oyster sauce","Garlic","Lettuce, tomato, onion","Lime and black pepper"]',
 '["Marinate the beef cubes for 15 minutes.","Sear on very high heat.","Serve on lettuce with the lime-pepper dip."]'),
('Nom Banh Chok','នំបញ្ចុក','Fresh rice noodles with a herbal fish gravy.','មីនំបញ្ចុកជាមួយសម្លត្រីគ្រឿងសមុទ្រ។',380,'KH','noodles',60,4,'hard',true,
 '["Fresh rice noodles","300 g fish","Lemongrass, turmeric, galangal","Coconut milk","Banana flower, cucumber, herbs"]',
 '["Boil and flake the fish.","Pound the herb paste and simmer with coconut milk.","Pour the gravy over noodles and add fresh vegetables."]'),
('Kuy Teav','គុយទាវ','Pork noodle soup with a clear, light broth.','គុយទាវសាច់ជ្រូក ជាមួយទឹកសម្លស្រាល។',350,'KH','noodles',40,2,'easy',false,
 '["Rice noodles","200 g pork","Pork bones for broth","Garlic oil","Bean sprouts, spring onion"]',
 '["Simmer bones for broth.","Cook the noodles and place in a bowl.","Top with pork and pour hot broth over."]'),
('Bai Sach Chrouk','បាយសាច់ជ្រូក','Grilled marinated pork over broken rice.','បាយសាច់ជ្រូកអាំងជាមួយបាយបំបែក។',560,'KH','rice',30,2,'easy',false,
 '["300 g pork slices","Coconut milk","Garlic","Fish sauce, sugar","Cooked rice","Pickled vegetables"]',
 '["Marinate pork with coconut milk, garlic and sugar.","Grill until charred at the edges.","Serve over rice with pickles."]'),
('Samlor Machu Trey','សម្លម្ជូរត្រី','Sour fish soup with pineapple and tamarind.','សម្លម្ជូរត្រីជាមួយម្នាស់និងអំពិល។',250,'KH','soup',35,4,'easy',false,
 '["400 g fish","Tamarind paste","Pineapple","Tomato","Morning glory or herbs"]',
 '["Boil water with tamarind and pineapple.","Add fish and tomato.","Finish with herbs and fish sauce."]'),
('Khmer Chicken Curry','សម្លការីមាន់','Mild coconut curry with potato and chicken.','សម្លការីមាន់ដូងស្រាល ជាមួយដំឡូង។',480,'KH','curry',50,4,'medium',false,
 '["500 g chicken","Curry paste","Coconut milk","Potato, onion","Lemongrass"]',
 '["Fry the curry paste.","Add chicken and coconut milk.","Simmer with potato until tender. Serve with bread or rice."]'),
('Samlor Korko','សម្លកកូរ','Thick Khmer vegetable and pork soup with toasted rice.','សម្លកកូរសាច់ជ្រូកនិងបន្លែ។',390,'KH','soup',60,4,'hard',false,
 '["Pork","Toasted rice powder","Eggplant, pumpkin, green papaya","Kroeung paste","Herbs"]',
 '["Fry the kroeung paste with pork.","Add water and vegetables.","Thicken with toasted rice powder."]'),
('Num Kachay','នំកាឆាយ','Crisp chive cakes with a sweet-sour dip.','នំកាឆាយបំពង់ ញាំជាមួយទឹកជ្រលក់។',300,'KH','snack',40,4,'medium',false,
 '["Rice flour","Chinese chives","Shrimp or pork","Oil for frying","Dipping sauce"]',
 '["Mix flour and water into a batter and fold in chives.","Pan-fry until golden and crisp.","Serve hot with the dip."]'),
('Lemongrass Beef Stir-fry','ឆាគ្រឿងសាច់គោ','Beef stir-fried with lemongrass and chili.','សាច់គោឆាគ្រឿង។',430,'KH','main',25,2,'easy',false,
 '["250 g beef","Lemongrass","Chili","Garlic","Fish sauce","Oil"]',
 '["Slice beef thinly.","Fry garlic, lemongrass and chili.","Add beef and fish sauce and stir on high heat."]'),
('Chicken Fried Rice','បាយឆាមាន់','Quick wok rice with chicken, egg and tomato.','បាយឆាមាន់ជាមួយស៊ុតនិងប៉េងប៉ោះ។',520,'KH','rice',20,2,'easy',false,
 '["2 cups cooked rice","1 chicken breast","2 eggs","1 tomato","Fish sauce"]',
 '["Fry the diced chicken.","Scramble the eggs in the wok.","Add rice and tomato and stir for 3 minutes."]'),
('Pho Bo','ភ្វើ','Vietnamese beef noodle soup with a spiced broth.','ស៊ុបមីសាច់គោវៀតណាម។',420,'VN','noodles',90,4,'hard',false,
 '["Beef bones","Rice noodles","Star anise, cinnamon, ginger","Beef slices","Herbs and lime"]',
 '["Simmer bones with spices for hours.","Cook noodles and place in bowls.","Add raw beef slices and pour boiling broth over."]');
